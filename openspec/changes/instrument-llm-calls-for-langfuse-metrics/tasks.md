## 1. Add gen_ai instrumentation to agent runtime

- [x] 1.1 In `McpAgentRuntime.RunAsync`, wrap the `state.Agent.RunAsync(message)` call in a child Activity with `gen_ai.*` attributes. Set `langfuse.observation.type` to `"generation"`, `gen_ai.system` to `"openai"`, `gen_ai.request.model` to the configured model name, `gen_ai.request.body` to the serialized message, and `gen_ai.response.body` to the response text. Set `gen_ai.usage.input_tokens` and `gen_ai.usage.output_tokens` to 0 as baseline (MAF's `RunAsync` does not expose usage counts). Verify the activity is created as a child of `bankingapp.agent.run` and appears in Langfuse as a GENERATION observation.

## 2. Build verification

- [x] 2.1 Run `dotnet build --configuration Release` from the repository root. Verify zero compilation errors.

## 3. Integration test

- [ ] 3.1 Run the BankingApp locally with Langfuse credentials from `.env`, send a chat request, and verify a GENERATION observation appears in Langfuse trace detail view with model name, input content, and output content populated.