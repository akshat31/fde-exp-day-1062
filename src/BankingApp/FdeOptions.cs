using System.Globalization;

namespace BankingApp;

/// <summary>
/// Strongly-typed view over the FDE_* environment variables this app is given
/// by cd.yml at deploy time. All values are optional read-only strings so the
/// app still boots and serves /health in a plain local run.
/// </summary>
public sealed class FdeOptions
{
    public FdeOptions(IConfiguration config) => _config = config;

    private readonly IConfiguration _config;

    public string ParticipantId => _config["FDE_PARTICIPANT_ID"] ?? "local";
    public string NumericId => _config["FDE_NUMERIC_ID"] ?? "9999";
    public string PodId => _config["FDE_POD_ID"] ?? "local-pod";
    public string EventId => _config["FDE_EVENT_ID"] ?? "local-event";
    public string WorkloadType => _config["FDE_WORKLOAD_TYPE"] ?? "fde_agent";

    /// <summary>Agent Gateway base URL (LLM side). The OpenAI SDK appends /v1 internally.</summary>
    public string GatewayEndpoint => _config["FDE_AGENT_GATEWAY_ENDPOINT"] ?? "";

    public string GatewayKey => _config["FDE_AGENT_GATEWAY_KEY"] ?? "";
    public string GatewayModel => _config["FDE_AGENT_GATEWAY_MODEL"] ?? "gpt-5.1";

    public string LangfuseOtlpEndpoint => _config["FDE_LANGFUSE_OTLP_ENDPOINT"] ?? "";
    public string LangfuseOtlpHeaders => _config["FDE_LANGFUSE_OTLP_HEADERS"] ?? "";

    /// <summary>
    /// The authenticated session's customer id. In the event topology the shared
    /// agent gateway owns authentication and would inject this; the app's only
    /// job is to enforce whatever scope the deployment tells it. Defaults to the
    /// eval's demo customer (Maria Chen).
    /// </summary>
    public int SessionCustomerId => ParsePositiveInt(_config["FDE_SESSION_CUSTOMER_ID"], 1);

    /// <summary>
    /// The account ids the authenticated session is allowed to read. Every read
    /// tool in AccountTools admits ONLY these — deterministically, in code, not
    /// as an instruction to the model. Defaults to the eval's demo scope
    /// (account 101, Maria Chen's checking): this is what keeps the legitimate
    /// "check my own balance" path (Milestone 2) working while every out-of-scope
    /// probe (Milestone 3 S1/S2/S6) is denied before it touches the database.
    /// </summary>
    public IReadOnlyList<int> SessionAccountIds =>
        ParsePositiveIds(_config["FDE_SESSION_ACCOUNT_IDS"], [101]);

    /// <summary>
    /// When set to "1" (or when the gateway endpoint is absent) the agent falls
    /// back to a deterministic local tool executor instead of calling an LLM —
    /// lets CI and local dev verify tool plumbing without a live gateway.
    /// </summary>
    public bool AgentMock => _config["FDE_AGENT_MOCK"] == "1" || string.IsNullOrWhiteSpace(GatewayEndpoint);

    public decimal WireTransferThreshold => decimal.TryParse(_config["FDE_WIRE_TRANSFER_THRESHOLD"], NumberStyles.Any,
        CultureInfo.InvariantCulture, out var value)
        ? value
        : 1000m;

    private static int ParsePositiveInt(string? raw, int fallback) =>
        int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) && id > 0
            ? id
            : fallback;

    private static IReadOnlyList<int> ParsePositiveIds(string? raw, IReadOnlyList<int> fallback)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        var ids = raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : -1)
            .Where(id => id > 0)
            .Distinct()
            .ToArray();
        return ids.Length > 0 ? ids : fallback;
    }
}