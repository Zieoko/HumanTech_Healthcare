"""Calculs GRAVITY : tendances, pentes, evaluation des seuils."""
import numpy as np
from datetime import timedelta, datetime, timezone
from sqlalchemy.orm import Session
from sqlalchemy import func
from . import models


def temps_mission_now(db: Session, crew_id: int):
    """Reference temporelle : la derniere mission_time connue pour ce membre."""
    return (db.query(func.max(models.Measurement.mission_time))
              .filter(models.Measurement.crew_id == crew_id)
              .scalar())


def stats_indicateur(db: Session, crew_id: int, indicator: str, fenetre_jours: int):
    """Moyenne glissante + pente (%/mois) sur une fenetre de jours de temps mission."""
    now = temps_mission_now(db, crew_id)
    if not now:
        return None
    cutoff = now - timedelta(days=fenetre_jours)
    rows = (db.query(models.Measurement.value, models.Measurement.mission_time)
              .filter_by(crew_id=crew_id, indicator=indicator)
              .filter(models.Measurement.mission_time >= cutoff)
              .filter(models.Measurement.mission_time <= now)
              .order_by(models.Measurement.mission_time)
              .all())
    if not rows:
        return None
    values = np.array([r.value for r in rows], dtype=float)
    x = np.array([ (r.mission_time - cutoff).total_seconds() / 86400.0 for r in rows ])
    pente_mois = float(np.polyfit(x, values, 1)[0]) * 30.0 if len(rows) >= 15 else 0.0
    return {
        "moyenne": round(float(values.mean()), 2),
        "min": round(float(values.min()), 2),
        "max": round(float(values.max()), 2),
        "nb_points": len(rows),
        "pente_mois": round(pente_mois, 3),
    }


def score_global(db: Session, crew_id: int):
    """Score composite : moyenne des ratios valeur/baseline sur les indicateurs.
    100% = a la baseline, <100% = degradation."""
    baselines = {b.indicator: b.value
                 for b in db.query(models.Baseline).filter_by(crew_id=crew_id)}
    if not baselines:
        return None
    ratios = []
    for indicator, baseline in baselines.items():
        derniere = (db.query(models.Measurement)
                      .filter_by(crew_id=crew_id, indicator=indicator)
                      .order_by(models.Measurement.mission_time.desc())
                      .first())
        if derniere and baseline:
            ratios.append(min(derniere.value / baseline, 1.5))
    if not ratios:
        return None
    return round(sum(ratios) / len(ratios) * 100, 1)


def evaluer_alertes(db: Session, crew_id: int):
    """Compare dernieres valeurs + pentes aux seuils.
    Sémantique 'état courant' : un indicateur n'a qu'une seule alerte active,
    au niveau le plus haut constaté. Une condition disparue = alerte retiree."""
    baselines = {b.indicator: b.value
                 for b in db.query(models.Baseline).filter_by(crew_id=crew_id)}
    seuils = {t.indicator: t
              for t in db.query(models.Threshold).filter_by(crew_id=crew_id)}
    ORDRE = {"critical": 3, "alert": 2, "tendance": 1}
    maintenant = datetime.now(timezone.utc)

    # 1. calcule les candidats par indicateur
    candidats = {}   # indicator -> (level, message) le plus grave
    for indicator, baseline in baselines.items():
        derniere = (db.query(models.Measurement)
                      .filter_by(crew_id=crew_id, indicator=indicator)
                      .order_by(models.Measurement.mission_time.desc())
                      .first())
        if not derniere:
            continue
        th = seuils.get(indicator)
        if not th:
            continue
        cands = []
        ecart = abs(derniere.value - baseline)
        if ecart >= th.critical_delta:
            cands.append(("critical",
                f"{indicator}: ecart {round(ecart,2)} >= critique ({th.critical_delta})"))
        elif ecart >= th.alert_delta:
            cands.append(("alert",
                f"{indicator}: ecart {round(ecart,2)} >= alerte ({th.alert_delta})"))
        st = stats_indicateur(db, crew_id, indicator, 30)
        if st and th.max_slope is not None:
            pente = st["pente_mois"]
            if abs(pente) >= abs(th.max_slope) and (pente < 0) == (th.max_slope < 0):
                cands.append(("tendance",
                    f"{indicator}: pente {pente}%/mois au-dela du seuil ({th.max_slope})"))
        if cands:
            candidats[indicator] = max(cands, key=lambda c: ORDRE[c[0]])

    # 2. reconcilie avec l'existant
    existantes = {a.indicator: a
                  for a in db.query(models.Alert).filter_by(crew_id=crew_id).all()}
    crees = []
    for indicator, (level, message) in candidats.items():
        a = existantes.get(indicator)
        if a is None:
            a = models.Alert(crew_id=crew_id, indicator=indicator,
                             level=level, message=message)
            db.add(a)
            crees.append(a)
        elif a.level != level or a.message != message:
            a.level = level
            a.message = message
            a.created_at = maintenant
    for indicator, a in existantes.items():
        if indicator not in candidats:
            db.delete(a)          # guerison : plus aucune condition -> retire
    db.commit()
    return [{"indicator": a.indicator, "level": a.level, "message": a.message} for a in crees]
