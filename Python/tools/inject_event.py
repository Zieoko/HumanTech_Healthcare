"""Injection d'evenements de mission dans GRAVITY.
Usage :
    python tools/inject_event.py --crew 2 --type injury --payload '{"location": "genou droit", "severity": "moderate"}'
    python tools/inject_event.py --crew 1 --type equipment_failure --payload '{"equipment": "tapis_course"}'
"""
import argparse
import json
from datetime import datetime, timezone

import requests

API = "http://localhost:8000"

def main():
    p = argparse.ArgumentParser(description="Injecte un evenement de mission")
    p.add_argument("--crew", type=int, required=True, help="id du membre (1, 2, 3)")
    p.add_argument("--type", required=True,
                   help="injury | fatigue | illness | equipment_failure | workload")
    p.add_argument("--payload", default="{}",
                   help='JSON, ex : \'{"location": "genou"}\'')
    args = p.parse_args()

    payload = json.loads(args.payload)
    r = requests.post(f"{API}/events", json={
        "crew_id": args.crew,
        "type": args.type,
        "timestamp": datetime.now(timezone.utc).isoformat(),
        "payload": payload,
    }, timeout=10)
    r.raise_for_status()
    print(r.json())

if __name__ == "__main__":
    main()
