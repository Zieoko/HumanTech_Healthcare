from sqlalchemy.orm import Session
from . import models

EXERCICES = {
    "bone_density": [
        {"nom": "Musculation en resistance (haltères, elastiques)",
         "dose": "3×/semaine, 30 min", "intensite": "moderee a forte", "impact": False},
        {"nom": "Sauts pliometriques controles",
         "dose": "2×/semaine, 15 min", "intensite": "moderee", "impact": True},
    ],
    "muscle_index": [
        {"nom": "Renforcement musculaire general (machines, poids du corps)",
         "dose": "4×/semaine, 45 min", "intensite": "forte", "impact": False},
        {"nom": "Circuit training adapte",
         "dose": "2×/semaine, 30 min", "intensite": "moderee", "impact": True},
    ],
    "vo2max": [
        {"nom": "Velo ou rameur (cardio continu)",
         "dose": "3×/semaine, 40 min", "intensite": "moderee", "impact": False},
        {"nom": "Fractionne sur tapis",
         "dose": "2×/semaine, 20 min", "intensite": "forte", "impact": True},
    ],
    "heart_rate_rest": [
        {"nom": "Cardio leger regulier (velo, marche cotee)",
         "dose": "4×/semaine, 30 min", "intensite": "leger", "impact": False},
    ],
    "spo2": [
        {"nom": "Exercices respiratoires et ventilation controlee",
         "dose": "2×/jour, 10 min", "intensite": "tres leger", "impact": False},
        {"nom": "Cardio doux en endurance",
         "dose": "3×/semaine, 30 min", "intensite": "leger", "impact": False},
    ],
    "recovery_score": [
        {"nom": "Sommeil : plage fixe 8h, pas d'ecran 1h avant",
         "dose": "quotidien", "intensite": "—", "impact": False},
        {"nom": "Etirements et recuperation active",
         "dose": "4×/semaine, 20 min", "intensite": "tres leger", "impact": False},
    ],
}

ZONES_IMPACT = ["genou", "cheville", "hanche", "pied"]

SUBSTITUTS_IMPACT = {
    "bone_density": "Velo avec forte resistance (charge osseuse sans impact)",
    "muscle_index": "Musculation en resistance lente (meme stimulus, sans impact)",
    "vo2max": "Velo en fractionne (cardio intense sans impact)",
}


def generer_plan(db: Session, crew_id: int) -> dict:
    """Construit le plan de remise en forme a partir des alertes et blessures."""
    crew = db.get(models.CrewMember, crew_id)
    if not crew:
        return {"erreur": f"membre id={crew_id} inconnu"}

    alertes = (db.query(models.Alert)
                 .filter(models.Alert.crew_id == crew_id)
                 .filter(models.Alert.level.in_(["critical", "tendance"]))
                 .order_by(models.Alert.created_at.desc())
                 .all())
    indicateurs = []
    for a in alertes:
        if a.indicator not in indicateurs:
            indicateurs.append(a.indicator)

    blessures = []
    impact_interdit = False
    evts = (db.query(models.Event)
              .filter_by(crew_id=crew_id, type="injury")
              .order_by(models.Event.mission_time.desc())
              .limit(10).all())
    for e in evts:
        loc = (e.payload or {}).get("location", "")
        if loc:
            blessures.append(loc)
            if any(z in loc.lower() for z in ZONES_IMPACT):
                impact_interdit = True

    plan = {}
    substitutions = []
    for ind in indicateurs:
        exercices = []
        for ex in EXERCICES.get(ind, []):
            if impact_interdit and ex["impact"]:
                continue
            exercices.append(ex)
        if impact_interdit and ind in SUBSTITUTS_IMPACT:
            substitutions.append(f"{ind} : {SUBSTITUTS_IMPACT[ind]}")
        if exercices:
            plan[ind] = exercices

    return {
        "crew_id": crew_id,
        "name": crew.name,
        "indicateurs_en_alerte": indicateurs,
        "blessures": blessures,
        "impact_interdit": impact_interdit,
        "plan": plan,
        "substitutions": substitutions,
        "note": "Plan genere par GRAVITY Core. La decision medicale finale revient au medecin de bord.",
    }
