using System.Text.Json.Serialization;

namespace GravityDashboard;

public class Crew
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("role")] public string? Role { get; set; }

    public override string ToString() => Name;
}

public class Indicateur
{
    [JsonPropertyName("valeur")] public double Valeur { get; set; }
    [JsonPropertyName("baseline")] public double? Baseline { get; set; }
    [JsonPropertyName("ecart_abs")] public double? EcartAbs { get; set; }
    [JsonPropertyName("ecart_pct")] public double? EcartPct { get; set; }
}

public class CrewStatus
{
    [JsonPropertyName("crew_id")] public int CrewId { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("score_global")] public double? ScoreGlobal { get; set; }
    [JsonPropertyName("indicateurs")] public Dictionary<string, Indicateur> Indicateurs { get; set; } = new();
}

public class AlertItem
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("crew_id")] public int CrewId { get; set; }
    [JsonPropertyName("indicator")] public string Indicator { get; set; } = "";
    [JsonPropertyName("level")] public string Level { get; set; } = "";
    [JsonPropertyName("message")] public string Message { get; set; } = "";
}

// Conversation avec l'IA

public class ChatMessage
{
    [JsonPropertyName("role")] public string Role { get; set; } = "";
    [JsonPropertyName("content")] public string Content { get; set; } = "";
}

public class ChatReply
{
    [JsonPropertyName("reply")] public string Reply { get; set; } = "";
}

// Ligne du tableau des indicateurs

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
        Ind = kv.Key;
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
