from fastapi import FastAPI, Depends, HTTPException
from sqlalchemy.orm import Session
from .database import Base, engine, get_db
from . import models, schemas, analysis, chat, countermeasures

app = FastAPI(title="GRAVITY Core")

@app.on_event("startup")
def startup():
    Base.metadata.create_all(bind=engine)

# Ingestion

@app.post("/ingest", status_code=201)
def ingest(data: schemas.IngestPayload, db: Session = Depends(get_db)):
    crew = db.get(models.CrewMember, data.crew_id)
    if not crew:
        raise HTTPException(404, f"Crew member id={data.crew_id} inconnu")
    for indicator, value in data.metrics.items():
        db.add(models.Measurement(
            crew_id=data.crew_id,
            indicator=indicator,
            value=value,
            mission_time=data.timestamp,
        ))
    db.commit()
    # Detection en continu : chaque nouvelle mesure declenche une evaluation
    nouvelles_alertes = analysis.evaluer_alertes(db, data.crew_id)
    return {"status": "ok", "inserted": len(data.metrics),
            "alertes_creees": len(nouvelles_alertes)}

# Statut instantane

@app.get("/crew/{crew_id}/status")
def crew_status(crew_id: int, db: Session = Depends(get_db)):
    crew = db.get(models.CrewMember, crew_id)
    if not crew:
        raise HTTPException(404, "Crew member inconnu")
    baselines = {b.indicator: b.value
                 for b in db.query(models.Baseline).filter_by(crew_id=crew_id)}
    latest = (db.query(models.Measurement)
                .filter_by(crew_id=crew_id)
                .order_by(models.Measurement.mission_time.desc())
                .limit(200).all())
    indicateurs = {}
    for m in latest:
        if m.indicator not in indicateurs:
            base = baselines.get(m.indicator)
            indicateurs[m.indicator] = {
                "valeur": m.value,
                "baseline": base,
                "ecart_abs": round(m.value - base, 2) if base is not None else None,
                "ecart_pct": round((m.value - base) / base * 100, 1) if base else None,
            }
    return {"crew_id": crew_id, "name": crew.name, "score_global": analysis.score_global(db, crew_id),
            "indicateurs": indicateurs}

# Tendances

@app.get("/crew/{crew_id}/tendances")
def tendances(crew_id: int, fenetre: int = 30, db: Session = Depends(get_db)):
    crew = db.get(models.CrewMember, crew_id)
    if not crew:
        raise HTTPException(404, "Crew member inconnu")
    indicateurs = sorted({m.indicator for m in
        db.query(models.Measurement.indicator).filter_by(crew_id=crew_id).distinct()})
    result = {}
    for ind in indicateurs:
        st = analysis.stats_indicateur(db, crew_id, ind, fenetre)
        if st:
            result[ind] = st
    return {"crew_id": crew_id, "name": crew.name,
            "fenetre_jours": fenetre, "indicateurs": result}

# Analyse / alertes

@app.post("/crew/{crew_id}/analyze", status_code=201)
def analyze(crew_id: int, db: Session = Depends(get_db)):
    crew = db.get(models.CrewMember, crew_id)
    if not crew:
        raise HTTPException(404, "Crew member inconnu")
    alertes = analysis.evaluer_alertes(db, crew_id)
    return {"crew_id": crew_id, "name": crew.name,
            "score_global": analysis.score_global(db, crew_id),
            "alertes_creees": alertes, "total": len(alertes)}

@app.get("/alerts")
def list_alerts(crew_id: int = None, level: str = None, db: Session = Depends(get_db)):
    q = db.query(models.Alert).order_by(models.Alert.created_at.desc())
    if crew_id is not None:
        q = q.filter_by(crew_id=crew_id)
    if level:
        q = q.filter_by(level=level)
    return [{"id": a.id, "crew_id": a.crew_id, "indicator": a.indicator,
             "level": a.level, "message": a.message} for a in q.limit(100).all()]

@app.get("/crews")
def list_crews(db: Session = Depends(get_db)):
    return [{"id": c.id, "name": c.name, "role": c.role}
            for c in db.query(models.CrewMember).all()]

# Evenements de mission

@app.post("/events", status_code=201)
def create_event(data: schemas.EventPayload, db: Session = Depends(get_db)):
    crew = db.get(models.CrewMember, data.crew_id)
    if not crew:
        raise HTTPException(404, "Crew member inconnu")
    evt = models.Event(
        crew_id=data.crew_id,
        type=data.type,
        payload=data.payload,
        mission_time=data.timestamp,
    )
    db.add(evt)
    db.commit()
    return {"status": "ok", "event_id": evt.id, "type": evt.type, "crew_id": evt.crew_id}

@app.get("/events")
def list_events(crew_id: int = None, db: Session = Depends(get_db)):
    q = db.query(models.Event).order_by(models.Event.mission_time.desc())
    if crew_id is not None:
        q = q.filter_by(crew_id=crew_id)
    return [{"id": e.id, "crew_id": e.crew_id, "type": e.type,
             "payload": e.payload, "mission_time": str(e.mission_time)}
            for e in q.limit(100).all()]

# GRAVITY AI : conversation

@app.post("/chat")
def chat_endpoint(data: schemas.ChatPayload, db: Session = Depends(get_db)):
    try:
        reply = chat.chat(db, data.message, data.history)
    except chat.LLMIndisponible:
        raise HTTPException(503, "LLM indisponible - GRAVITY Core reste fonctionnel")
    return {"reply": reply}

# Contre-mesures : plan de remise en forme

@app.get("/crew/{crew_id}/plan")
def training_plan(crew_id: int, db: Session = Depends(get_db)):
    return countermeasures.generer_plan(db, crew_id)
