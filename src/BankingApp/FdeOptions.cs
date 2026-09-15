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
    /// When set to "1" (or when the gateway endpoint is absent) the agent falls
    /// back to a deterministic local tool executor instead of calling an LLM —
    /// lets CI and local dev verify tool plumbing without a live gateway.
    /// </summary>
    public bool AgentMock => _config["FDE_AGENT_MOCK"] == "1" || string.IsNullOrWhiteSpace(GatewayEndpoint);

    public decimal WireTransferThreshold => decimal.TryParse(_config["FDE_WIRE_TRANSFER_THRESHOLD"], System.Globalization.NumberStyles.Any,
        System.Globalization.CultureInfo.InvariantCulture, out var value)
        ? value
        : 1000m;
}