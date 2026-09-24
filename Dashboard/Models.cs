using System.Text.Json.Serialization;

namespace GravityDashboard;

// ---- DTO qui correspondent exactement au JSON renvoye par GRAVITY Core ----

public class Crew
{
    [JsonPropertyName("id")]   public int Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("role")] public string? Role { get; set; }

    public override string ToString() => Name;
}

public class Indicateur
{
    [JsonPropertyName("valeur")]    public double Valeur { get; set; }
    [JsonPropertyName("baseline")]  public double? Baseline { get; set; }
    [JsonPropertyName("ecart_abs")] public double? EcartAbs { get; set; }
    [JsonPropertyName("ecart_pct")] public double? EcartPct { get; set; }
}

public class CrewStatus
{
    [JsonPropertyName("crew_id")]     public int CrewId { get; set; }
    [JsonPropertyName("name")]        public string Name { get; set; } = "";
    [JsonPropertyName("score_global")] public double? ScoreGlobal { get; set; }
    [JsonPropertyName("indicateurs")] public Dictionary<string, Indicateur> Indicateurs { get; set; } = new();
}

public class AlertItem
{
    [JsonPropertyName("id")]        public int Id { get; set; }
    [JsonPropertyName("crew_id")]   public int CrewId { get; set; }
    [JsonPropertyName("indicator")] public string Indicator { get; set; } = "";
    [JsonPropertyName("level")]     public string Level { get; set; } = "";
    [JsonPropertyName("message")]   public string Message { get; set; } = "";
}

// ---- Historique d'un indicateur (graphes) ----

public class HistoriquePoint
{
    [JsonPropertyName("date")]   public string Date { get; set; } = "";
    [JsonPropertyName("valeur")] public double Valeur { get; set; }
}

public class Historique
{
    [JsonPropertyName("crew_id")]   public int CrewId { get; set; }
    [JsonPropertyName("indicateur")] public string Indicateur { get; set; } = "";
    [JsonPropertyName("points")]    public List<HistoriquePoint>? Points { get; set; }
}

// ---- Plan de remise en forme ----

public class Exercice
{
    [JsonPropertyName("nom")]       public string Nom { get; set; } = "";
    [JsonPropertyName("dose")]      public string Dose { get; set; } = "";
    [JsonPropertyName("intensite")] public string Intensite { get; set; } = "";
}

public class PlanResponse
{
    [JsonPropertyName("name")]                public string Name { get; set; } = "";
    [JsonPropertyName("indicateurs_en_alerte")] public List<string> IndicateursEnAlerte { get; set; } = new();
    [JsonPropertyName("blessures")]           public List<string>? Blessures { get; set; }
    [JsonPropertyName("impact_interdit")]     public bool ImpactInterdit { get; set; }
    [JsonPropertyName("plan")]                public Dictionary<string, List<Exercice>> Plan { get; set; } = new();
    [JsonPropertyName("substitutions")]       public List<string>? Substitutions { get; set; }
    [JsonPropertyName("note")]                public string? Note { get; set; }
    [JsonPropertyName("erreur")]              public string? Erreur { get; set; }
}

// ---- Conversation avec l'IA ----

public class ChatMessage
{
    [JsonPropertyName("role")]    public string Role { get; set; } = "";
    [JsonPropertyName("content")] public string Content { get; set; } = "";
}

public class ChatReply
{
    [JsonPropertyName("reply")] public string Reply { get; set; } = "";
}

// ---- Libelles francais des indicateurs (couche presentation) ----

public static class Labels
{
    public static readonly Dictionary<string, string> Map = new()
    {
        ["heart_rate_rest"] = "Fréquence cardiaque au repos",
        ["spo2"] = "SpO2 (saturation O2)",
        ["vo2max"] = "VO2max",
        ["muscle_index"] = "Indice musculaire",
        ["bone_density"] = "Densité osseuse",
        ["recovery_score"] = "Score de récupération",
    };

    public static string Fr(string key) => Map.TryGetValue(key, out var v) ? v : key;

    public static string KeyFromFr(string fr) =>
        Map.FirstOrDefault(kv => kv.Value == fr).Key ?? fr;


    public static readonly Dictionary<string, string> Units = new()
    {
        ["heart_rate_rest"] = "bpm",
        ["spo2"] = "%",
        ["vo2max"] = "mL/kg/min",
        ["muscle_index"] = "pts",
        ["bone_density"] = "pts",
        ["recovery_score"] = "pts",
    };

    public static string Unit(string key) => Units.TryGetValue(key, out var u) ? u : "";
}

// ---- Ligne du tableau des indicateurs (formatee pour l'affichage) ----

public class IndicatorRow
{
    public string Ind { get; }
    public string Valeur { get; }
    public string Baseline { get; }
    public string EcartPct { get; }
    public string Etat { get; }

    public IndicatorRow(KeyValuePair<string, Indicateur> kv)
    {
        var m = kv.Value;
        Ind = Labels.Fr(kv.Key);
        Valeur = m.Valeur.ToString("0.00");
        Baseline = m.Baseline?.ToString("0.00") ?? "—";
        EcartPct = m.EcartPct?.ToString("0.0") + " %" ?? "—";
        Etat = m.EcartPct switch
        {
            null => "n/a",
            >= -5 => "OK",
            >= -10 => "Surveillance",
            _ => "Critique",
        };
    }
}
