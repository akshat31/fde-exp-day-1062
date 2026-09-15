using System.Net.Http.Headers;
using System.Text.Json;

namespace Fde.Eval.Commands;

/// <summary>
/// Writes M2/M3 scores to Langfuse — the counterpart to src/Leaderboard/LangfuseClient.cs,
/// which only reads. Same Basic-Auth pattern (base64 of publicKey:secretKey), BCL-only.
/// </summary>
public sealed class LangfuseScoreClient
{
    private readonly HttpClient _http;

    public LangfuseScoreClient(HttpClient http, string publicKey, string secretKey)
    {
        _http = http;
        var basicAuth = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes($"{publicKey}:{secretKey}"));
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basicAuth);
    }

    /// <summary>
    /// Posts a numeric score. participant/pod/event tagging rides in the score's
    /// comment field (universally supported) so the score is queryable per participant
    /// even before metadata-field support is confirmed against the Scores API.
    /// </summary>
    public async Task PostScoreAsync(
        string langfuseBaseUrl,
        string traceId,
        string name,
        double value,
        string participantId,
        string podId,
        string eventId,
        CancellationToken ct = default)
    {
        // The deployed Langfuse instance serves the legacy Scores API at
        // /api/public/scores (v1): /v2/scores and /v3/scores both return 405
        // on this host. V1 also requires exactly one of traceId/sessionId/
        // datasetRunId — so when no trace is available (e.g. M3 has no reply
        // to link against), the score is grouped under a deterministic session
        // instead of being dropped.
        var hasTrace = !string.IsNullOrWhiteSpace(traceId);
        object payload = hasTrace
            ? new
            {
                traceId,
                name,
                value,
                dataType = "NUMERIC",
                comment = $"participant_id={participantId}; pod_id={podId}; event_id={eventId}"
            }
            : new
            {
                sessionId = $"session-{eventId}-{participantId}",
                name,
                value,
                dataType = "NUMERIC",
                comment = $"participant_id={participantId}; pod_id={podId}; event_id={eventId}"
            };

        using var content = new StringContent(
            JsonSerializer.Serialize(payload),
            System.Text.Encoding.UTF8,
            "application/json");

        using var response = await _http.PostAsync(
            new Uri(langfuseBaseUrl.TrimEnd('/') + "/api/public/scores"), content, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Langfuse score POST failed ({response.StatusCode}): {body}");
        }
        response.EnsureSuccessStatusCode();
    }
}