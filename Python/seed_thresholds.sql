-- Seuils d alerte pour les 3 membres (ecarts absolus + pente max %/mois)
INSERT INTO thresholds (crew_id, indicator, alert_delta, critical_delta, max_slope) VALUES
(1, 'heart_rate_rest', 5, 10, 2),   (1, 'spo2', 2, 5, -0.5),
(1, 'vo2max', 3, 6, -1.0),          (1, 'muscle_index', 3, 6, -0.8),
(1, 'bone_density', 1, 2, -0.5),    (1, 'recovery_score', 8, 15, -2.0),
(2, 'heart_rate_rest', 5, 10, 2),   (2, 'spo2', 2, 5, -0.5),
(2, 'vo2max', 3, 6, -1.0),          (2, 'muscle_index', 3, 6, -0.8),
(2, 'bone_density', 1, 2, -0.5),    (2, 'recovery_score', 8, 15, -2.0),
(3, 'heart_rate_rest', 5, 10, 2),   (3, 'spo2', 2, 5, -0.5),
(3, 'vo2max', 3, 6, -1.0),          (3, 'muscle_index', 3, 6, -0.8),
(3, 'bone_density', 1, 2, -0.5),    (3, 'recovery_score', 8, 15, -2.0);
