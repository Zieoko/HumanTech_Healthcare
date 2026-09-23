"""Simulateur GRAVITY : genere des mois de mesures pour les 3 membres
d'equipage, avec profils de derive differents, et les envoie a l'API.
A executer sur la VM (ou ailleurs) :  python tools/simulate.py
"""
import random
import requests
from datetime import datetime, timedelta, timezone

API = "http://localhost:8000"
JOURS = 180
random.seed(42)  # reproductibilite des demos

# Profils par membre : baselines legerement differentes
PROFILS = {
    1: {"name": "Crew01",
        "baselines": {"heart_rate_rest": 58.0, "spo2": 98.0, "vo2max": 45.0,
                      "muscle_index": 90.0, "bone_density": 100.0, "recovery_score": 80.0},
        "derive": {"heart_rate_rest": +0.5, "spo2": -0.15, "vo2max": -0.8,
                   "muscle_index": -1.2, "bone_density": -0.9, "recovery_score": -0.3}},
    2: {"name": "Crew02",
        "baselines": {"heart_rate_rest": 62.0, "spo2": 97.0, "vo2max": 41.0,
                      "muscle_index": 85.0, "bone_density": 100.0, "recovery_score": 74.0},
        "derive": {"heart_rate_rest": +0.8, "spo2": -0.25, "vo2max": -1.1,
                   "muscle_index": -1.6, "bone_density": -1.4, "recovery_score": -0.6}},
    3: {"name": "Crew03",
        "baselines": {"heart_rate_rest": 55.0, "spo2": 99.0, "vo2max": 48.0,
                      "muscle_index": 93.0, "bone_density": 100.0, "recovery_score": 84.0},
        "derive": {"heart_rate_rest": +0.3, "spo2": -0.10, "vo2max": -0.5,
                   "muscle_index": -0.7, "bone_density": -0.5, "recovery_score": -0.2}},
}

def main():
    debut = datetime(2027, 1, 1, tzinfo=timezone.utc)
    for crew_id, profil in PROFILS.items():
        for d in range(JOURS):
            t = debut + timedelta(days=d)
            mois_ecoules = d / 30.0
            metrics = {}
            for ind, base in profil["baselines"].items():
                derive = profil["derive"][ind] * mois_ecoules
                bruit = random.gauss(0, abs(base) * 0.01 + 0.1)
                val = round(base + derive + bruit, 2)
                if ind == "spo2":
                    val = min(val, 100.0)
                metrics[ind] = val
            r = requests.post(f"{API}/ingest", json={
                "crew_id": crew_id,
                "timestamp": t.isoformat(),
                "metrics": metrics,
            }, timeout=10)
            r.raise_for_status()
        print(f"{JOURS} jours simules pour {profil['name']} (id={crew_id})")

if __name__ == "__main__":
    main()
