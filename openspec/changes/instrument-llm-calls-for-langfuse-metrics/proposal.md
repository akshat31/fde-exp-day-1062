## Why

The BankingApp sends OTLP trace spans to Langfuse v4.4.0 but LLM calls to the agent gateway are not instrumented with `gen_ai.*` telemetry attributes. The Langfuse dashboard shows traces as bare SPAN-type observations with null `input`/`output` and zero token counts — the LLM generation data (model, prompt, completion, usage) never reaches the observability platform. Scores and session/user metadata are present and correctly linked, but without instrumented LLM calls the dashboard has no rich trace content to display.

## What Changes

- Add `gen_ai.*` OpenTelemetry attribute tags to spans around LLM calls in the BankingApp agent runtime, so Langfuse renders them as GENERATION observations with model name, input/output content, and token usage
- The existing OTLP exporter pipeline already sends to Langfuse — only the span content needs enrichment

## Capabilities

### New Capabilities
- `llm-call-instrumentation`: LLM calls in the BankingApp agent SHALL be instrumented with OpenTelemetry `gen_ai.*` tracing attributes so Langfuse displays rich trace data with model, prompt, completion, and token usage

## Impact

- `src/BankingApp/Agent/McpAgentRuntime.cs` — wrap LLM calls in instrumented Activities
- `src/BankingApp/Agent/BankingAgentFactory.cs` — may need changes to expose ActivitySource