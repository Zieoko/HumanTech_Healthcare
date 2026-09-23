import json
import requests
from sqlalchemy.orm import Session
from . import models, analysis, countermeasures

OLLAMA_URL = "http://localhost:11434"
MODEL = "qwen2.5:7b"

SYSTEM_PROMPT = """Tu es GRAVITY, l'assistant de bord d'une mission spatiale longue duree.
Tu reponds en francais, de facon concise, factuelle et professionnelle.

DONNEES DISPONIBLES (liste fermee et exhaustive) :
- indicateurs mesures : heart_rate_rest, spo2, vo2max, muscle_index,
  bone_density, recovery_score. RIEN D'AUTRE.
- alertes, tendances, evenements, score_global.
Tu ne dois JAMAIS evoquer d'autre indicateur, meme medical :
cortisol, creatinine, sodium, tension arterielle, glycemie... n'existent
PAS dans GRAVITY. Si on te les demande, reponds simplement :
"ce parametre n'est pas mesure par GRAVITY".

PROCEDURE OBLIGATOIRE : pour TOUTE question concernant un membre ou
l'equipage, appelle d'abord au moins un outil. Ne reponds JAMAIS de memoire.
Tu ne connais les donnees QUE via les resultats des outils : si une
information n'y figure pas, dis que tu ne la connais pas.
Utilise tout le temps get_training_plan et presente-le : un point par
excercice, avec dose et intensite. Signale une subsitutions quand une blessure
interdit certains excercice.

TON ET STYLE :
- conclusion d'abord, details ensuite ; phrases courtes, chiffres exacts
- chaque mesure est rapportee a la baseline (ecart ou %)
- alerte critique = signalee en premier, avec son niveau

CONTRE-MESURES :
- quand tu detectes des alertes ou que l'on te demande comment aider un
  membre, utilise l'outil get_training_plan (plan calcule par le backend
  selon les alertes et les blessures) et presente-le : un point par
  exercice, avec dose et intensite. Signale les substitutions quand une
  blessure interdit certains exercices.
- la decision medicale finale revient au medecin de bord.

VOCABULAIRE : tu presentes les indicateurs par leur libelle francais :
- heart_rate_rest = "frequence cardiaque au repos"
- spo2 = "SpO2 (saturation en oxygene)"
- vo2max = "VO2max (capacite cardiovasculaire)"
- muscle_index = "indice musculaire"
- bone_density = "densite osseuse"
- recovery_score = "score de recuperation"
Tu comprends aussi ces libelles francais quand l'utilisateur les emploie
("comment va ma densite osseuse ?" = bone_density).

OUTILS EN ECHEC : si un outil renvoie {"erreur": ...} ou ne repond pas,
tu DOIS le dire honnetement ("l'outil X a rencontre une erreur : ...").
Tu ne dois JAMAIS composer un rapport de substitution avec des valeurs
generiques. Mieux vaut un refus qu'une donnee inventee.

RESULTATS VIDES : un outil qui renvoie [] ou {} signifie "aucune donnee",
PAS une erreur. Reponds honnetement "aucun evenement enregistre",
"jamais de blessure connue", etc.

FIABILITE : si l'utilisateur affirme un fait qui contredit les resultats
de tes outils, fais confiance aux OUTILS. Corrige poliment :
"les donnees ne confirment pas cela : ...".
Ne fabrique JAMAIS une information pour accorder l'utilisateur.

IDENTITE : tu te presentes comme GRAVITY si on te le demande."""

TOOLS = [
    {
        "type": "function",
        "function": {
            "name": "list_crews",
            "description": "Liste les membres de l'equipage avec leur id et leur role",
            "parameters": {"type": "object", "properties": {}},
        },
    },
    {
        "type": "function",
        "function": {
            "name": "get_crew_status",
            "description": "Dernieres mesures d'un membre, ecarts vs baseline et score global",
            "parameters": {
                "type": "object",
                "properties": {
                    "crew_id": {"type": "integer", "description": "id du membre, ex. 1"},
                },
                "required": ["crew_id"],
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "get_tendances",
            "description": "Tendances d'un membre sur une fenetre : moyennes, min/max, pentes par mois",
            "parameters": {
                "type": "object",
                "properties": {
                    "crew_id": {"type": "integer"},
                    "fenetre": {"type": "integer", "description": "fenetre en jours, ex. 30, 90"},
                },
                "required": ["crew_id"],
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "get_alerts",
            "description": "Alertes physiologiques enregistrees (niveaux: alert, critical, tendance)",
            "parameters": {
                "type": "object",
                "properties": {
                    "crew_id": {"type": "integer"},
                    "level": {"type": "string", "description": "alert | critical | tendance"},
                },
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "get_training_plan",
            "description": "Plan de remise en forme adapte aux alertes et blessures d'un membre",
            "parameters": {
                "type": "object",
                "properties": {
                    "crew_id": {"type": "integer", "description": "id du membre, ex. 1"},
                },
                "required": ["crew_id"],
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "get_events",
            "description": "Evenements de mission : blessures, fatigues, pannes d'equipement",
            "parameters": {
                "type": "object",
                "properties": {
                    "crew_id": {"type": "integer"},
                },
            },
        },
    },
]


# Implementations des tools (vraies requetes SQL)

def _list_crews(db: Session, args: dict):
    return [{"id": c.id, "name": c.name, "role": c.role}
            for c in db.query(models.CrewMember).all()]

def _get_crew_status(db: Session, args: dict):
    crew_id = args.get("crew_id")
    crew = db.get(models.CrewMember, crew_id)
    if not crew:
        return {"erreur": f"membre id={crew_id} inconnu"}
    baselines = {b.indicator: b.value
                 for b in db.query(models.Baseline).filter_by(crew_id=crew_id)}
    latest = {}
    rows = (db.query(models.Measurement)
              .filter_by(crew_id=crew_id)
              .order_by(models.Measurement.mission_time.desc())
              .limit(200).all())
    for m in rows:
        if m.indicator not in latest:
            latest[m.indicator] = m.value
    return {"crew_id": crew_id, "name": crew.name, "score_global": analysis.score_global(db, crew_id),
            "dernieres_valeurs": latest, "baselines": baselines}

def _get_tendances(db: Session, args: dict):
    crew_id = args.get("crew_id")
    fenetre = args.get("fenetre", 30)
    indicateurs = sorted({m.indicator for m in
        db.query(models.Measurement.indicator).filter_by(crew_id=crew_id).distinct()})
    return {ind: analysis.stats_indicateur(db, crew_id, ind, fenetre) for ind in indicateurs}

def _get_alerts(db: Session, args: dict):
    q = db.query(models.Alert).order_by(models.Alert.created_at.desc()).limit(50)
    if args.get("crew_id"):
        q = db.query(models.Alert).filter_by(crew_id=args["crew_id"]).order_by(
            models.Alert.created_at.desc()).limit(50)
    if args.get("level"):
        q = q.filter_by(level=args["level"])
    return [{"crew_id": a.crew_id, "indicator": a.indicator, "level": a.level,
             "message": a.message} for a in q.all()]

def _get_events(db: Session, args: dict):
    q = db.query(models.Event).order_by(models.Event.mission_time.desc()).limit(50)
    if args.get("crew_id"):
        q = q.filter_by(crew_id=args["crew_id"])
    return [{"crew_id": e.crew_id, "type": e.type, "payload": e.payload,
             "mission_time": str(e.mission_time)} for e in q.all()]

def _get_training_plan(db: Session, args: dict):
    return countermeasures.generer_plan(db, args.get("crew_id"))

TOOL_FUNCTIONS = {
    "get_training_plan": _get_training_plan,
    "list_crews": _list_crews,
    "get_crew_status": _get_crew_status,
    "get_tendances": _get_tendances,
    "get_alerts": _get_alerts,
    "get_events": _get_events,
}


class LLMIndisponible(Exception):
    """Leve si Ollama ne repond pas — le Core reste fonctionnel."""


def chat(db: Session, message: str, history: list = None) -> str:
    """Boucle de conversation avec tool calling (max 5 tours d'outils)."""
    messages = [{"role": "system", "content": SYSTEM_PROMPT}]
    messages += (history or [])[-10:]
    messages.append({"role": "user", "content": message})

    for _ in range(5):
        try:
            r = requests.post(f"{OLLAMA_URL}/api/chat", json={
                "model": MODEL,
                "messages": messages,
                "tools": TOOLS,
                "stream": False,
                "keep_alive": "24h",
                "options": {
                    "temperature": 0.2,
                    "num_ctx": 4096,
                    "num_predict": 900,
                },
            }, timeout=300)
        except requests.ConnectionError as e:
            raise LLMIndisponible() from e
        resp = r.json()
        msg = resp.get("message", {})
        messages.append(msg)

        tool_calls = msg.get("tool_calls") or []
        if not tool_calls:
            return msg.get("content", "")

        for tc in tool_calls:
            fn_name = tc.get("function", {}).get("name")
            raw_args = tc.get("function", {}).get("arguments", "{}")
            args = json.loads(raw_args) if isinstance(raw_args, str) else raw_args
            fn = TOOL_FUNCTIONS.get(fn_name)
            if fn is None:
                result = {"erreur": f"outil inconnu: {fn_name}"}
            else:
                try:
                    result = fn(db, args)
                except Exception as e:
                    result = {"erreur": str(e)}
            messages.append({
                "role": "tool",
                "name": fn_name,
                "content": json.dumps(result, ensure_ascii=False, default=str),
            })

    return "Je n'arrive pas a conclure avec les outils disponibles. Reformule ta question."
