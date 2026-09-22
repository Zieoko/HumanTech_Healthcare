

# GRAVITY

## Système autonome de suivi physiologique pour mission spatiale longue durée

### Objectif

GRAVITY est un système **HealthTech autonome et hors ligne** destiné à
surveiller l'évolution physique d'un équipage pendant une mission
spatiale longue durée.

Il compare les données physiologiques de chaque membre à une **baseline
individuelle enregistrée avant le départ**, détecte les dérives et
permet d'interroger les résultats grâce à une **IA locale**.

------------------------------------------------------------------------

## Fonctionnement

### 1. Baseline pré-mission

Avant le départ, GRAVITY enregistre pour chaque membre : fréquence
cardiaque au repos ; SpO2 / capacité cardiovasculaire (VO2max) ;
indice musculaire ; densité osseuse ; score de récupération / sommeil.

Cette baseline sert de référence pendant toute la mission.

> *À préciser : liste définitive des indicateurs simulés. Les
> indicateurs à évolution lente (densité osseuse) et rapide (fatigue)
> n'ont pas la même granularité de mesure.*

### 2. Suivi physiologique

Pour le prototype, les données sont **simulées**, générées par un
Arduino Yùn qui joue le rôle de collecteur de capteurs.

GRAVITY stocke leur évolution et calcule notamment : l'écart avec la
baseline ; les tendances sur 7 / 30 / 90 jours ; les dégradations
progressives ; la fatigue et la récupération.

### 3. Contre-mesures physiques

Le système peut adapter un programme d'exercices selon : l'évolution
musculaire, osseuse et cardiovasculaire ; la fatigue ; les blessures
; les équipements disponibles ; les événements de mission.

Exemple : une blessure au genou entraîne l'exclusion des exercices
incompatibles et la génération d'alternatives.

### 4. GRAVITY AI — chatbot et LLM

**Il n'y a qu'une seule IA dans le système.** Trois rôles, un seul
pipeline :

| Rôle | Composant | Rôle exact |
|------|-----------|------------|
| Fenêtre de conversation | Chatbot (dans le dashboard C#) | Interface utilisateur : affiche les questions et réponses |
| Orchestrateur | GRAVITY Core (FastAPI) | Ajoute les tools, exécute les calculs, protège la BDD |
| Moteur + cerveau | Ollama + LLM local (ex. Qwen 14B quantifié) | Comprend la question, formule la réponse en langage naturel |

Ollama est le logiciel qui **fait tourner** le modèle localement
(comme Docker pour LLM) ; le LLM est le cerveau ; le chatbot est
simplement l'UI de conversation. Pas deux IA distinctes.

Le LLM **ne réalise pas les calculs critiques** et n'accède pas
directement à la base de données. Le backend reste la source de vérité.

#### Boucle de conversation (tool calling)

```
Utilisateur → "GRAVITY, comment va Crew01 ?"
        │
        ▼
FastAPI reçoit le message, l'envoie à Ollama avec les tools disponibles
        │
        ▼
Le LLM demande l'appel d'un tool, ex. get_crew_status(crew01)
        │
        ▼
FastAPI exécute le tool (requête PostgreSQL) → vraies données
        │
        ▼
Le LLM rédige la réponse en langage naturel
        │
        ▼
Le dashboard C# affiche la réponse
```

Ollama supporte le tool calling (format compatible OpenAI). Le
dashboard C# ne voit qu'un seul endpoint `/chat` ; le LLM ne touche
jamais la base.

------------------------------------------------------------------------

## Architecture

``` text
              Poste équipage (Windows)
                       │
              Dashboard C# (Avalonia ou WPF)
              Données crew + graphes + chat
                       │
                  HTTP (REST + /chat)
                       │
                VM GRAVITY
                       │
        ┌──────────────┼──────────────┐
        │              │              │
   FastAPI/Core     Ollama/LLM    PostgreSQL
        ▲
        │ POST /ingest (JSON)
        │
   Raspberry Pi
   (simulateur capteurs)
```

> *À trancher : framework C# définitif — Avalonia (multiplateforme,
> peut tourner sur la VM elle-même) ou WPF (Windows pur, plus mature).*

### Stack envisagée

  Composant         Technologie
  ----------------- ------------------------------------
  VM                Ubuntu Server (12–16 Go RAM)
  Backend           Python + FastAPI
  Base de données   PostgreSQL
  IA locale         Ollama + LLM quantifié (14B Q4, ou 7B si RAM limitée)
  Appli             C# (dashboard + chatbot)
  Simulation        Python sur Raspberry Pi

------------------------------------------------------------------------

## Ingestion des données (Raspberry Pi)

Le Raspberry Pi joue le rôle de collecteur de capteurs (simulés dans
le prototype). Il **ne parle jamais directement à PostgreSQL** : il
envoie du JSON à un endpoint FastAPI, qui valide puis écrit en base.

### Contrat d'API

``` json
POST /ingest
{
  "crew_id": "crew01",
  "timestamp": "2027-03-14T08:30:00Z",
  "metrics": {
    "heart_rate_rest": 58,
    "spo2": 97,
    "vo2max": 42.1,
    "muscle_index": 88.4,
    "bone_density": -2.1,
    "recovery_score": 76
  },
  "event": null
}
```

Réponse : `201 Created` ou `400 Bad Request` avec le détail de
l'erreur de validation.

### Exigences côté Pi

- **File d'attente locale** (SQLite ou JSONL sur carte SD) : si le
  POST échoue (réseau coupé), les mesures sont stockées et renvoyées
  en batch (`POST /ingest/batch`)
- **Authentification** : token API statique dans le header
  (`Authorization: Bearer <token>`)
- **Double horodatage** : le JSON porte un `timestamp` **mission**
  (temps simulé, pour l'accélération de mission) et le backend stocke
  aussi un `timestamp_reception` **réel** — les deux sont distincts
  en base, sinon les tendances sont faussées

> *Alternative en V2 : MQTT (broker Mosquitto sur la VM) si le projet
> exige du broadcast IoT ou plusieurs abonnés. HTTP retenu pour le MVP
> : plus simple à déboguer, testable avec curl.*

------------------------------------------------------------------------

## Données principales

``` text
crew_members
baselines
measurements
training_sessions
training_programs
injuries
events
equipment
alerts
thresholds        ← seuils configurables par membre et par indicateur
```

Ollama n'accède jamais directement à PostgreSQL :

``` text
Utilisateur → LLM → Tools/API → GRAVITY Core → PostgreSQL
```

------------------------------------------------------------------------

## Détection des dérives

La détection vit **entièrement dans FastAPI**, pas dans le LLM
(déterministe, testable, reproductible). Le LLM lit seulement le
résultat et le reformule en langage naturel.

Trois niveaux de détection :

**Niveau 1 — Écart instantané** (bruit ou problème aigu)

```
alerte si |valeur_actuelle - baseline| > seuil_configurable
ex. SpO2 < 92 % → alerte immédiate
```

**Niveau 2 — Pente / tendance** (le vrai indicateur de dérive en
microgravité)

```
régression linéaire sur les 30 derniers jours
alerte si pente significative ET écart > X % de la baseline
ex. densité osseuse : -0,8 %/mois pendant 3 mois → dérive confirmée
```

**Niveau 3 — Score composite** (la synthèse pour le chatbot)

```
score_global par membre = moyenne pondérée des écarts normalisés
→ permet de répondre "Crew01 est à 76 % de sa baseline"
```

### Outils retenus (statistique classique, pas de ML)

| Outil | Usage | Coût |
|-------|-------|------|
| `psycopg2` + SQL | écarts, moyennes glissantes | zéro dépendance |
| `numpy` (`polyfit`) | régressions linéaires (pentes) | une ligne de pip |
| `pydantic` | validation des seuils | déjà dans FastAPI |

Les seuils sont **stockés en base** (table `thresholds`), pas en dur
dans le code — réglables pendant la démo sans redéployer.

------------------------------------------------------------------------

## Simulation

Le prototype permet d'accélérer une mission de plusieurs années.

Des événements peuvent être injectés : blessure ; fatigue importante
; mauvaise récupération ; maladie simulée ; panne d'un équipement
sportif ; modification de la charge de travail.

GRAVITY analyse alors l'impact de l'événement et adapte le suivi.

> *À prévoir : un endpoint ou une CLI d'injection dédiée, ex.*
> `POST /events` *ou* `gravity-cli inject --type injury --crew 01`
> *— c'est ce qui rendra la démo interactive.*

------------------------------------------------------------------------

## Principe de résilience

GRAVITY est conçu **offline-first**.

Même sans Internet, le système doit continuer à : enregistrer les
données ; calculer les tendances ; détecter les anomalies ;
afficher le dashboard ; conserver l'historique.

Si le LLM devient indisponible, **GRAVITY Core continue de
fonctionner** — le chatbot affiche un message, le dashboard reste
alimenté par les données du backend.

### Mesures de résilience appliquées

- **File locale sur le Pi** : aucune mesure perdue si le réseau tombe
- **Sauvegardes PostgreSQL** : `pg_dump` cron quotidien + copie du
  dump hors VM (remontée possible en ~30 min)
- **Horodatage mission / réel séparés** : l'accélération de mission
  ne corrompt pas l'historique

------------------------------------------------------------------------

## MVP

Le prototype minimum comprend :

1.  3 membres d'équipage simulés ;
2.  une baseline individuelle par membre ;
3.  5 à 6 indicateurs physiologiques ;
4.  un simulateur longue durée sur Raspberry Pi (POST HTTP vers
    FastAPI, file locale, batch retry) ;
5.  PostgreSQL pour l'historique ;
6.  FastAPI pour la logique métier ;
7.  détection des dérives à trois niveaux (écart, pente, score
    composite) avec seuils configurables en base ;
8.  programme physique adaptatif simple ;
9.  gestion de quelques événements injectables (endpoint `/events`) ;
10. Ollama + un seul LLM local, intégré au chatbot via tool calling
    orchestré par FastAPI ;
11. dashboard C# + chatbot ;
12. fonctionnement entièrement hors ligne sur une VM (12–16 Go RAM).

------------------------------------------------------------------------

## Phasage suggéré

1. **Phase 1** : Core FastAPI + PostgreSQL + simulateur + baseline +
   calculs d'écarts (sans LLM, sans dashboard).
2. **Phase 2** : dashboard C# + contre-mesures adaptatives.
3. **Phase 3** : Ollama + chatbot + injection d'événements.
4. **Phase 4** : polish démo (scénarios pré-scriptés, branding).

------------------------------------------------------------------------

## Synthèse

> **GRAVITY est un système autonome qui surveille l'évolution
> physiologique de chaque membre d'équipage par rapport à son état
> pré-mission, adapte les contre-mesures physiques et permet
> d'interroger les données grâce à une IA locale, sans dépendre de la
> Terre.**

### Démonstration cible

1.  Lancer une mission simulée (depuis le Raspberry Pi).
2.  Faire défiler plusieurs mois/années.
3.  Observer une dérive physiologique (détection multi-niveaux).
4.  GRAVITY détecte et analyse la tendance.
5.  Interroger le chatbot sur la situation.
6.  Injecter une blessure ou une panne.
7.  Montrer l'adaptation du système.
8.  Couper Internet et démontrer que GRAVITY reste opérationnel
    (file locale sur le Pi, Core indépendant du LLM).

