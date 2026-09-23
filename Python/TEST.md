# Test rapide GRAVITY Core

## Prerequis
    sudo apt install ollama (ou: curl -fsSL https://ollama.com/install.sh | sh)
    ollama pull qwen2.5:14b

## Lancer
    cd /opt/gravity
    source venv/bin/activate
    uvicorn app.main:app --host 0.0.0.0 --port 8000

## Donnees de test
    psql "postgresql://gravity:root@localhost/gravity" -f seed.sql
    psql "postgresql://gravity:root@localhost/gravity" -f seed_thresholds.sql
    python tools/simulate.py
    curl -X POST http://localhost:8000/crew/1/analyze
    curl -X POST http://localhost:8000/crew/2/analyze
    python tools/inject_event.py --crew 2 --type injury --payload '{"location": "genou droit", "severity": "moderate"}'

## GRAVITY AI (premier appel: 20-40 s, chargement du modele en RAM)
    curl -X POST http://localhost:8000/chat -H "Content-Type: application/json" \
      -d '{"message": "GRAVITY, comment va Crew02 ?"}'

    curl -X POST http://localhost:8000/chat -H "Content-Type: application/json" \
      -d '{"message": "Quelles sont les alertes critiques actuelles ?"}'

    curl -X POST http://localhost:8000/chat -H "Content-Type: application/json" \
      -d '{"message": "Y a-t-il eu des evenements recents ?"}'

## Resilience : couper Ollama puis re-demander -> 503 propre
    sudo systemctl stop ollama

## Endpoints
    GET  /crews | /crew/{id}/status | /crew/{id}/tendances
    POST /crew/{id}/analyze | /ingest | /events | /chat
    GET  /alerts?crew_id=2&level=critical | /events?crew_id=2
    GET  /docs
