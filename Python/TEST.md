# Test rapide GRAVITY Core

## Lancer
    cd /opt/gravity
    source venv/bin/activate
    uvicorn app.main:app --host 0.0.0.0 --port 8000

## Donnees de test (2e terminal, venv active)
    psql "postgresql://gravity:root@localhost/gravity" -f seed.sql
    psql "postgresql://gravity:root@localhost/gravity" -f seed_thresholds.sql
    python tools/simulate.py          # 3 crews x 180 jours

## Endpoints
    GET  /crews                       # liste des membres
    GET  /crew/1/status               # ecarts + score global
    GET  /crew/1/tendances?fenetre=90 # moyennes + pentes
    POST /crew/1/analyze              # cree les alertes
    GET  /alerts?crew_id=2&level=critical
    POST /ingest                      # {"crew_id": 1, "timestamp": "...", "metrics": {...}}
    GET  /docs                        # documentation interactive
