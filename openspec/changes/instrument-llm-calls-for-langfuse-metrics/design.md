## Context

The BankingApp sends OTLP traces to Langfuse v4 via `OpenTelemetry.Exporter.OpenTelemetryProtocol`. The traces show `bankingapp.chat` and `bankingapp.agent.run` spans (type=SPAN) but the actual LLM call — `state.Agent.RunAsync(message)` in `McpAgentRuntime.cs:75` — produces no gen_ai instrumentation. Langfuse renders these as empty spans with null input/output and zero tokens.

The MAF agent uses `Azure.AI.OpenAI` v2 (`OpenAIClient`) to call the agent gateway, wrapped as an `IChatClient`. There is no automatic .NET gen_ai instrumentation for this stack — instrumentation must be added manually per the Langfuse OTel .NET integration guide.

## Goals / Non-Goals

**Goals:**
- Wrap the LLM agent call in an Activity with `gen_ai.*` attributes so Langfuse renders GENERATION observations
- Capture token usage (input/output) from the LLM response metadata
- Capture prompt and completion content for trace debugging

**Non-Goals:**
- Adding an automatic gen_ai instrumentation NuGet package (none exists for .NET)
- Changing the `AIAgent` or `IChatClient` pipeline structure
- Instrumenting the MCP tool calls (already captured as child spans)

## Decisions

### Decision 1: Manual Activity wrapping with gen_ai attributes

**Rationale:** Langfuse's .NET OTel integration does not have an automatic LLM instrumentation library. The documented approach is to create spans manually with `gen_ai.*` attributes. The BankingApp already uses `ActivitySource` from `BankingActivitySources` for its existing spans. Wrapping `agent.RunAsync()` in a new Activity is the simplest path.

**Alternatives considered:**
- **IChatClient middleware (OpenTelemetryChatClient):** Would require creating a delegating `IChatClient` wrapper. More code for the same outcome; the agent internally creates its own client chain that we'd need to intercept
- **Langfuse .NET SDK:** No official .NET SDK exists; only Python/JS. Can't use
- **Microsoft.Extensions.AI OpenTelemetry integration:** The `Microsoft.Extensions.AI` package has chat client instrumentation but exports to the same OTLP pipeline; equivalent complexity to manual wrapping

### Decision 2: Extend ActivitySource with a gen_ai span under the agent.run parent

**Rationale:** The existing trace structure is `bankingapp.chat → bankingapp.agent.run`. Adding a child span under `bankingapp.agent.run` for the LLM call creates the natural hierarchy. Langfuse recognizes child spans of an agent-run span as observations and uses `gen_ai.*` attributes to render them as GENERATION type.

**Implementation:** In `McpAgentRuntime.RunAsync`, after the existing `bankingapp.agent.run` activity starts, before `state.Agent.RunAsync(message)`, create a child activity with gen_ai tags. After the call completes, record token usage from the MAF agent's response metadata.

### Decision 3: Expose token usage from the AIAgent response

**Rationale:** The MAF `AIAgent` exposes `RunAsync()` which returns an `AgentResponse` containing the text. Token usage is available through `AgentResponse.Messages` — specifically `ChatCompletion` messages in the message history carry `Usage` data. However, capturing this requires accessing internal MAF types. A simpler approach: set the gen_ai activity tags before and after the call, and extract usage from the `IChatClient`'s last completion if accessible, or set token counts to 0 when unavailable (still valuable for input/output capture).

## Risks / Trade-offs

- **Token counts may be unavailable:** MAF's `AIAgent.RunAsync()` returns text only, not usage metadata. → Mitigation: token attributes default to 0 if the response doesn't expose usage; input/output content capture still provides value
- **gen_ai.request.body may be large:** Including full prompt + tool definitions as a span attribute can be verbose. → Mitigation: Langfuse handles large attributes; sampling/truncation can be added later if needed
- **Performance impact:** Additional Activity creation per LLM call adds minimal overhead (Activity is a lightweight struct)