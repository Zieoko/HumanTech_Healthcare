# Gravity

## Sommaire
1. [Objectif](#objectif)
2. [Fonctionnement](#fonctionnement)
3. [Architecture](#architecture)
4. [Arduino](#arduino)
5. [Data](#data)
6. [Détection des dérives](#détection-des-dérives)
7. [Simulation](#simulation)
8. [Principe de résilience](#principe-de-résilience)
9. [Synthèse](#synthèse)

## Objectif <a name="objectif"></a>

<p>
Une mission spatiale longue durée pose un problème médical concret. En microgravité, le corps humain se dégrade de façon lente et mesurable : un astronaute perd environ 1 % de densité osseuse par mois, sa masse musculaire fond et sa capacité cardiovasculaire diminue. Sur Terre, une équipe médicale surveillerait ces constantes en temps réel. Mais à plusieurs minutes de délai de communication, la surveillance doit vivre à bord, sans dépendre d'Internet ni d'un lien permanent.

GRAVITY est un système de santé connecté embarqué qui remplit trois missions principales :
1. Suivre l'évolution physiologique de chaque membre d'équipage par rapport à son état de référence mesuré avant le départ (la « baseline »).
2. Détecter les dérives avant qu'elles ne deviennent des problèmes (écart notable, tendance sur un mois, score de santé global).
3. Proposer des contre-mesures physiques adaptées via un plan d'entraînement correctif et répondre aux questions de l'équipage grâce à une intelligence artificielle fonctionnant entièrement à bord.
</p>

## Fonctionnement <a name="fonctionnement"></a>

<p>
Le système fonctionne en boucle continue pour assurer le suivi de l'équipage :
- Le collecteur envoie les mesures au cœur du système (GRAVITY Core), qui les valide puis les enregistre.
- À chaque mesure reçue, le système évalue automatiquement l'état de chaque membre.
- Le dashboard interroge le cœur en permanence pour afficher les métriques et alertes en temps réel.
- GRAVITY AI permet une interaction en langage naturel : lorsqu'une question est posée (ex. « comment va Crew02 ? »), l'IA demande au cœur d'exécuter une requête, puis rédige sa réponse avec les données réelles obtenues.
</p>

## Architecture <a name="architecture"></a>

<p>
GRAVITY s'articule autour de quatre composants principaux qui communiquent par des interfaces standard :

| Composant | Rôle | Technologie |
|---|---|---|
| **GRAVITY Core** | Cœur du système : reçoit les mesures, stocke l'historique, calcule les écarts, les tendances, les alertes et les plans d'entraînement. C'est la seule source de vérité. | API web (FastAPI) sur VM Debian |
| **Base de données** | Historique complet de la mission : membres, baselines pré-mission, mesures, seuils d'alerte, événements, alertes. | PostgreSQL |
| **GRAVITY AI** | Interface conversationnelle : répond aux questions en langage naturel à partir des vraies données. | Modèle de langage local (Qwen via Ollama) |
| **Dashboard** | Poste de commandement : état de l'équipage, écarts par rapport aux baselines, alertes colorées, fenêtre de chat. | Application de bureau (WPF C#) |
| **Collecteur** | Source des mesures : simulateur logiciel ou capteurs matériels. | Python / microcontrôleur |

**Flux global de données :**
```text
Arduino / simulateur
       |  mesures (JSON)
       v
GRAVITY Core (FastAPI)  <--- dashboard C# (lecture) + GRAVITY AI (questions)
       |
       v
PostgreSQL (historique complet de la mission)
```
</p>

## Arduino <a name="arduino"></a>

<p>
Dans la version réelle du système (ou prototype physique), un module Arduino ou Raspberry Pi sert de collecteur de données. Il capture les métriques physiologiques issues des capteurs embarqués et les transmet au GRAVITY Core sous forme de structures de données standardisées (JSON). Dans le cadre du développement et du prototypage, un simulateur logiciel Python peut se substituer à la partie matérielle.
</p>

## Data <a name="data"></a>

<p>
Chaque membre de l'équipage possède six indicateurs physiologiques clés :
- Fréquence cardiaque au repos
- Saturation en oxygène ($SpO_2$)
- Capacité cardiovasculaire ($VO_2max$)
- Indice musculaire
- Densité osseuse
- Score de récupération

**Baseline et double horodatage :**
- **Baseline :** Enregistrée avant le départ, elle constitue la valeur de référence unique par personne. La surveillance compare l'évolution à cette référence personnelle plutôt qu'à des normes génériques.
- **Double horodatage :** Chaque mesure enregistre deux dates : la date de la mission simulée et la date réelle d'arrivée dans le système. Cela permet d'accélérer la simulation sans corrompre le calcul des tendances.

### Modèle Conceptuel de Données (MCD)

| Entité | Description | Attributs principaux |
|---|---|---|
| **Membre de l'équipage** | Un astronaute suivi par le système. | `identifiant`, `nom` (Crew01…), `rôle` |
| **Baseline** | État physiologique de référence, mesuré avant le départ. | `indicateur`, `valeur de référence`, `date d'enregistrement` |
| **Mesure** | Une valeur relevée sur un indicateur, à une date de mission donnée. | `indicateur`, `valeur`, `date de mission`, `date de réception` |
| **Seuil d'alerte** | Paramètres de détection propres à chaque membre et indicateur. | `indicateur`, `écart avant alerte`, `écart avant critique`, `pente maximale tolérée` |
| **Événement** | Fait de mission : blessure, fatigue, panne d'équipement… | `type`, `détails`, `date de mission` |
| **Alerte** | État courant d'un indicateur dégradé, mis à jour en continu. | `indicateur`, `niveau`, `message`, `date de création` |
</p>

## Détection des dérives <a name="détection-des-dérives"></a>

<p>
La détection des dérives s'effectue automatiquement à chaque mesure reçue, selon trois niveaux d'analyse :

| Niveau | Principe | Exemple réel |
|---|---|---|
| **1. Écart instantané** | La dernière valeur s'éloigne de la baseline au-delà d'un seuil configurable (alerte ou critique). | $SpO_2$ sous 92 % $\rightarrow$ alerte immédiate. |
| **2. Tendance** | Une droite de tendance est calculée sur les 30 derniers jours ; si la pente dépasse le seuil toléré, la dérive est confirmée. | Densité osseuse : $-1,46\,\%/\text{mois}$ chez Crew02 (seuil : $-0,5\,\%$). |
| **3. Score composite** | Moyenne des écarts sur tous les indicateurs, ramenée sur une échelle de 100. | Crew01 : $95,3\,\%$ ; Crew02 : $93,9\,\%$ de sa baseline. |

Une alerte représente l'état courant d'un indicateur : une seule ligne par indicateur est conservée, mise à jour en continu, et retirée dès que la situation redevient normale.
</p>

## Simulation <a name="simulation"></a>

<p>
Afin de valider le fonctionnement sur des missions de longue durée sans attendre plusieurs mois, le système intègre un mode de simulation de temps accéléré. Grâce au principe du double horodatage (date de mission vs date système), il est possible de simuler plusieurs mois de données physiologiques en quelques secondes tout en conservant des calculs de tendances et de pentes parfaitement exacts sur le temps de mission.
</p>

## Principe de résilience <a name="principe-de-résilience"></a>

<p>
Le système est conçu selon le principe **« offline-first »** pour garantir la sécurité de l'équipage :
- **Autonomie locale :** Aucune donnée ne quitte la machine, aucun accès à Internet n'est requis.
- **Fonctionnement dégradé :** En cas de panne ou d'arrêt du module GRAVITY AI, le cœur (GRAVITY Core), la base de données et le dashboard restent 100 % opérationnels.
- **Calculs déterministes :** Les plans d'entraînement et les détections de dérives sont calculés par le cœur de manière déterministe et indépendante de l'IA.
- **IA sous contrôle (Tool Calling) :** Le modèle de langage (Qwen 14B via Ollama) n'a pas d'accès direct à la base de données. Il doit passer par des outils stricts fournis par le Core. Si une donnée n'existe pas ou qu'un outil échoue, l'IA l'indique honnêtement sans inventer de faits.
- **Réseau chiffré :** Communication entre l'application et le serveur via un réseau privé chiffré (Tailscale).
</p>

## Synthèse <a name="synthèse"></a>

<p>
GRAVITY offre une solution de suivi médical autonome et hautement résiliente adaptée aux contraintes des vols spatiaux habités. En combinant un moteur de règles déterministe pour la détection préventive des dérives physiologiques et une IA conversationnelle locale opérant en mode restreint, le système garantit un suivi précis, sécurisé et totalement fonctionnel en environnement déconnecté.
</p>
