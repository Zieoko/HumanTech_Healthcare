using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace GravityDashboard;

/// <summary>
/// Client HTTP vers GRAVITY Core (FastAPI sur la VM).
/// </summary>
public class ApiClient
{
    private static readonly JsonSerializerOptions Opts = new() { PropertyNameCaseInsensitive = true };
    private readonly HttpClient _http;

    public ApiClient(string baseUrl)
    {
        _http = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(180) };
    }

    public async Task<List<Crew>> GetCrewsAsync()
        => await GetAsync<List<Crew>>("crews") ?? new List<Crew>();

    public async Task<CrewStatus?> GetStatusAsync(int crewId)
        => await GetAsync<CrewStatus>($"crew/{crewId}/status");

    public async Task<List<AlertItem>> GetAlertsAsync()
        => await GetAsync<List<AlertItem>>("alerts") ?? new List<AlertItem>();

    // Graphe : serie complete d'un indicateur sur N jours de mission
    public async Task<Historique?> GetHistoriqueAsync(int crewId, string indicateur, int jours = 90)
        => await GetAsync<Historique>($"crew/{crewId}/historique?indicateur={indicateur}&jours={jours}");

    // Plan de remise en forme (contre-mesures calculees par le backend)
    public async Task<PlanResponse?> GetPlanAsync(int crewId)
        => await GetAsync<PlanResponse>($"crew/{crewId}/plan");

    // Injection d'un evenement de mission (blessure, panne...)
    public async Task<string> PostEventAsync(int crewId, string type, string payloadJson)
    {
        Dictionary<string, JsonElement>? payloadObj;
        try
        {
            payloadObj = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(payloadJson);
        }
        catch (JsonException)
        {
            throw new InvalidOperationException(
                "Le détail doit être un JSON valide, ex : {\"location\":\"genou droit\",\"severity\":\"moderate\"}");
        }

        var body = JsonSerializer.Serialize(new
        {
            crew_id = crewId,
            type,
            timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            payload = payloadObj,
        });
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        var resp = await _http.PostAsync("events", content);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsStringAsync();
    }

    public async Task<string> ChatAsync(string message, List<ChatMessage> history)
    {
        var body = JsonSerializer.Serialize(new { message, history });
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        var resp = await _http.PostAsync("chat", content);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<ChatReply>(json, Opts)?.Reply ?? "(pas de réponse)";
    }

    private async Task<T?> GetAsync<T>(string path)
    {
        var resp = await _http.GetAsync(path);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(json, Opts);
    }
}
