## Purpose

Enriches BankingApp trace data with LLM generation details so Langfuse v4 renders traces with model information, prompt and completion content, and token usage metrics — making the dashboard show the full observability picture rather than empty SPAN skeletons.

## ADDED Requirements

### Requirement: LLM calls emit gen_ai OpenTelemetry attributes

Every LLM call made by the BankingApp agent SHALL be wrapped in an OpenTelemetry Activity that carries `gen_ai.*` attributes following the semantic convention recognized by Langfuse's OTLP ingestion endpoint.

The instrumentation SHALL record the following attributes on the Activity span:
- `gen_ai.system` set to the LLM provider identifier (e.g. `"openai"`)
- `gen_ai.request.model` set to the model name from configuration
- `gen_ai.usage.input_tokens` set to the prompt token count from the LLM response
- `gen_ai.usage.output_tokens` set to the completion token count from the LLM response
- `gen_ai.request.body` containing the serialized request (prompt + tool definitions)
- `gen_ai.response.body` containing the serialized response (completion text)

The instrumentation SHALL also set `langfuse.observation.type` to `"generation"` on the Activity to explicitly mark the span as a Langfuse generation observation.

#### Scenario: LLM call appears as generation in Langfuse trace view

- **WHEN** the BankingApp agent processes a chat request and calls the LLM via the agent gateway
- **THEN** the resulting trace in Langfuse contains a GENERATION observation with model name, token counts, and request/response content populated

#### Scenario: Mock mode skips instrumentation

- **WHEN** the agent is running in mock mode (no gateway configured or FDE_AGENT_MOCK=1)
- **THEN** no gen_ai instrumentation spans are emitted for the mock response

#### Scenario: Token usage from LLM response is captured

- **WHEN** the LLM returns a response with usage metadata (prompt_tokens, completion_tokens)
- **THEN** the Activity records `gen_ai.usage.input_tokens` and `gen_ai.usage.output_tokens` matching the LLM response values