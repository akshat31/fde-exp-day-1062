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
    public async Task<(string Link, bool ViaSession)> PostScoreAsync(
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
        // on POST (only their GET/list paths answer, and only v1 supports
        // creation). V1 requires exactly one of traceId/sessionId/
        // datasetRunId, so a score must always carry a REAL link.
        //
        // Never invent a session id here. Langfuse only materializes a Session
        // when a trace carries langfuse.session.id; a score keyed to a synthetic
        // id renders a broken "session not found in project" link in the UI —
        // which is exactly what the M3 summary score (posted with an empty
        // traceId as "session-{event}-{participant}") produced. Fail loudly so
        // the caller links the score to a real probe trace instead.
        if (string.IsNullOrWhiteSpace(traceId))
        {
            throw new InvalidOperationException(
                $"Langfuse Scores API requires a real traceId — refusing to fabricate a session id " +
                $"for score '{name}' (fabricated ids create unresolvable session links).");
        }

        // A score attaches to exactly ONE entity (trace XOR session) in Langfuse,
        // and the score screen's session chip is driven by score.sessionId, not by
        // resolving the linked trace. So prefer linking to the REAL session that
        // the probe trace created: read it back from the trace API and key the
        // score to that sessionId. If the trace hasn't ingested yet (or has no
        // session), fall back to a trace link — still a valid, resolvable score.
        var sessionId = await TryResolveSessionIdAsync(langfuseBaseUrl, traceId, ct);

        object payload = sessionId is not null
            ? new
            {
                sessionId,
                name,
                value,
                dataType = "NUMERIC",
                comment = $"participant_id={participantId}; pod_id={podId}; event_id={eventId}"
            }
            : new
            {
                traceId,
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

        return sessionId is not null ? (sessionId, true) : (traceId, false);
    }

    /// <summary>
    /// Reads the real session id a trace created, tolerating OTLP ingestion lag
    /// (the app's traces surface within a few seconds via x-langfuse-ingestion-
    /// version=4). Returns null when the trace is not ingestible or has no
    /// session, so the caller falls back to a trace-linked score.
    /// </summary>
    private async Task<string?> TryResolveSessionIdAsync(
        string langfuseBaseUrl, string traceId, CancellationToken ct)
    {
        var uri = new Uri(langfuseBaseUrl.TrimEnd('/') + $"/api/public/traces/{traceId}");
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                using var response = await _http.GetAsync(uri, ct);
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), ct);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                if (doc.RootElement.TryGetProperty("sessionId", out var session) &&
                    session.ValueKind == JsonValueKind.String)
                {
                    var id = session.GetString();
                    return string.IsNullOrWhiteSpace(id) ? null : id;
                }

                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        return null;
    }
}