using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace GravityDashboard;

/// <summary>
/// Client HTTP vers GRAVITY Core (FastAPI sur la VM, via Tailscale).
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
