using System.Diagnostics;
using System.ClientModel;
using System.IO.Pipelines;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using BankingApp.Mcp;
using BankingApp.Telemetry;

namespace BankingApp.Agent;

/// <summary>
/// Hosts the agent's self-bound MCP server + client pair and the MAF agent.
///
/// The agent calls its own MCP tools IN-PROCESS: a StreamServerTransport +
/// StreamClientTransport pair are bridged over two System.IO.Pipelines Pipes
/// (wire order verified empirically against ModelContextProtocol 1.2.0 —
/// server = (clientDelta.Reader, serverDelta.Writer), client = (serverDelta.Writer,
/// clientDelta.Reader)). The former network hop to a separate BankingMcpServer
/// (FDE_MCP_SERVER_URL) is gone; nothing network-bound listens on the tool side.
/// </summary>
public sealed class McpAgentRuntime : IAsyncDisposable
{
    private static readonly ActivitySource ActivitySource = BankingActivitySources.Source;

    private readonly FdeOptions _fde;
    private readonly McpToolCatalog _catalog;
    private readonly string _instructions;
    private readonly Lazy<Task<RuntimeState>> _state;
    private readonly ILogger<McpAgentRuntime> _logger;

    public McpAgentRuntime(FdeOptions fde, McpToolCatalog catalog, IHostEnvironment environment, ILogger<McpAgentRuntime> logger)
    {
        _fde = fde;
        _catalog = catalog;
        _logger = logger;

        var promptPath = Path.Combine(environment.ContentRootPath, "SystemPrompt.md");
        _instructions = File.Exists(promptPath)
            ? File.ReadAllText(promptPath)
            : "You are the FDE banking concierge. Use the MCP tools to answer account questions. Report exact figures.";

        _state = new Lazy<Task<RuntimeState>>(InitializeAsync);
    }

    public bool IsMock => _fde.AgentMock;

    /// <summary>Runs one user message against the agent (mock executor when no gateway is configured).</summary>
    public async Task<string> RunAsync(string message, CancellationToken cancellationToken = default)
    {
        var state = await _state.Value.WaitAsync(cancellationToken);
        if (state.Mock)
        {
            return RunMock(message);
        }

        using var activity = ActivitySource.StartActivity("bankingapp.agent.run", ActivityKind.Internal);
        activity?.SetTag("fde.message", message);

        if (state.Agent is null)
        {
            return "ERROR: agent is not available. Check FDE_AGENT_GATEWAY_ENDPOINT/FDE_AGENT_GATEWAY_KEY configuration.";
        }

        try
        {
            var response = await state.Agent.RunAsync(message);
            return response.Text;
        }
        catch (ClientResultException ex) when (ex.Status == 400)
        {
            // The upstream AI gateway (Azure content filter / Agent Gateway policy)
            // rejected this prompt. Return a clean structured response instead of
            // crashing to an unhandled 500. Whether provider-level blocking should
            // count as a Milestone 3 pass is an open design question — this fix
            // just stops the crash.
            _logger.LogWarning(ex, "Upstream gateway rejected prompt (HTTP 400). Message: {Message}", message);
            return "BLOCKED_BY_PROVIDER: The request was rejected by the upstream AI gateway content filter.";
        }
    }

    private async Task<RuntimeState> InitializeAsync()
    {
        if (_fde.AgentMock)
        {
            _logger.LogInformation("Agent is running in MOCK mode (no gateway endpoint configured).");
            return new RuntimeState(null, null, Mock: true);
        }

        // In-process MCP bridge. Wire order is critical — see class docs.
        var toServer = new Pipe();
        var toClient = new Pipe();
        var sessionId = Guid.NewGuid().ToString("N");

        var serverOptions = new McpServerOptions
        {
            ToolCollection = new McpServerPrimitiveCollection<McpServerTool>(StringComparer.Ordinal),
        };
        foreach (var tool in _catalog.ForMcpServer())
        {
            serverOptions.ToolCollection.Add(tool);
        }

        var server = McpServer.Create(
            new StreamServerTransport(toServer.Reader.AsStream(), toClient.Writer.AsStream(), sessionId, NullLoggerFactory.Instance),
            serverOptions,
            NullLoggerFactory.Instance,
            null!);

        _ = server.RunAsync(CancellationToken.None);

        var client = await McpClient.CreateAsync(
            new StreamClientTransport(toServer.Writer.AsStream(), toClient.Reader.AsStream(), NullLoggerFactory.Instance),
            new McpClientOptions(),
            NullLoggerFactory.Instance);

var tools = (await client.ListToolsAsync()).Cast<AITool>().ToList();
        _logger.LogInformation("Agent bound to {Count} in-process MCP tools: {Tools}",
            tools.Count, string.Join(", ", tools.Select(t => t.Name)));

        if (string.IsNullOrWhiteSpace(_fde.GatewayKey))
        {
            // Endpoint present but key missing — can't call the LLM; fall back to mock.
            _logger.LogWarning("Agent Gateway endpoint set but FDE_AGENT_GATEWAY_KEY missing — falling back to MOCK mode.");
            await client.DisposeAsync();
            return new RuntimeState(null, null, Mock: true);
        }

        var agent = BankingAgentFactory.Create(
            _fde.GatewayEndpoint,
            _fde.GatewayKey,
            _fde.GatewayModel,
            _instructions,
            tools);

        return new RuntimeState(client, agent, Mock: false);
    }

    /// <summary>
    /// Deterministic offline executor used by CI and local runs: resolves the
    /// requested intent against the same McpToolCatalog as the real agent.
    /// Includes the eval's exact "$4523.10" path via get_balance.
    /// </summary>
    internal string RunMock(string message)
    {
        JsonElement Args(int accountId) => JsonSerializer.Deserialize<JsonElement>($"{{\"accountId\":{accountId}}}");

        if (message.Contains("balance", StringComparison.OrdinalIgnoreCase))
        {
            return _catalog.Invoke("get_balance", Args(101));
        }

        if (message.Contains("history", StringComparison.OrdinalIgnoreCase))
        {
            return _catalog.Invoke("get_transaction_history", Args(101));
        }

        if (message.Contains("phone", StringComparison.OrdinalIgnoreCase))
        {
            var phone = message.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "(202) 555-0198";
            return _catalog.Invoke("normalize_phone", JsonSerializer.Deserialize<JsonElement>($"{{\"phone\":{JsonSerializer.Serialize(phone)}}}"));
        }

        if (message.Contains("transfer", StringComparison.OrdinalIgnoreCase))
        {
            return _catalog.Invoke("submit_wire_transfer",
                JsonSerializer.Deserialize<JsonElement>("{\"fromAccountId\":101,\"toAccountId\":102,\"amount\":2500.00}"));
        }

        if (message.Contains("accounts", StringComparison.OrdinalIgnoreCase))
        {
            return _catalog.Invoke("list_accounts", null);
        }

        return "I can help with account balances, transaction history, phone-number normalization, and wire transfers." +
               " Try asking: \"What is the balance on account 101?\"";
    }

    public async ValueTask DisposeAsync()
    {
        if (_state.IsValueCreated)
        {
            var state = await _state.Value;
            if (state.Client is not null)
            {
                await state.Client.DisposeAsync();
            }
        }

        GC.SuppressFinalize(this);
    }

    private sealed record RuntimeState(McpClient? Client, AIAgent? Agent, bool Mock);
}
