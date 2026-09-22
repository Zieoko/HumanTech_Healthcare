-- Equipage GRAVITY : 3 membres, ids entiers, noms CrewXX
INSERT INTO crew_members (id, name, role) VALUES
(1, 'Crew01', 'Commandant'),
(2, 'Crew02', 'Ingenieure de vol'),
(3, 'Crew03', 'Medecin de bord');

-- Baselines individuelles (doivent correspondre aux profils du simulateur)
INSERT INTO baselines (crew_id, indicator, value, recorded_at) VALUES
(1, 'heart_rate_rest', 58, now()), (1, 'spo2', 98, now()),
(1, 'vo2max', 45, now()), (1, 'muscle_index', 90, now()),
(1, 'bone_density', 100, now()), (1, 'recovery_score', 80, now()),
(2, 'heart_rate_rest', 62, now()), (2, 'spo2', 97, now()),
(2, 'vo2max', 41, now()), (2, 'muscle_index', 85, now()),
(2, 'bone_density', 100, now()), (2, 'recovery_score', 74, now()),
(3, 'heart_rate_rest', 55, now()), (3, 'spo2', 99, now()),
(3, 'vo2max', 48, now()), (3, 'muscle_index', 93, now()),
(3, 'bone_density', 100, now()), (3, 'recovery_score', 84, now());
