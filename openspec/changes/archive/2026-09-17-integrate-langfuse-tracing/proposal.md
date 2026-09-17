## Why

The current Langfuse score integration has three problems that degrade trace observability: (1) `PostScoreAsync` uses a 5-cycle polling loop with up to 10s of blocking delay per score to resolve session IDs from the trace API — this adds latency and complexity to every score post; (2) per-scenario scores (S1–S7) are only collected in-memory and never posted to Langfuse individually, leaving the dashboard blind to which adversarial scenario failed; (3) the CI workflow's PromptDefense step and CD workflow's M3 step lack explicit env mapping for Langfuse secret keys, making local validation and pipeline troubleshooting difficult.

## What Changes

- **Replace session-id polling with direct trace-ID scoring**: Remove the `TryResolveSessionIdAsync` 5-cycle polling loop from `LangfuseScoreClient.PostScoreAsync`. Map payloads directly to `traceId`, set `environment = "ci-cd-pipeline"`, and serialize participant/pod/event tags into a structured `metadata` JSON sub-object instead of a plain-text `comment` field.
- **Streamline `EvalRuntime.TryPostScoreAsync`**: Accept an optional `comment` string parameter; pipe it directly through to `PostScoreAsync`. Remove the internal call to the now-deleted `TryResolveSessionIdAsync`.
- **Inject per-scenario scores in M3 milestone loop**: Inside `Milestone3Command.RunAsync`'s `foreach (var scenario in BuildScenarios())` block, call `EvalRuntime.TryPostScoreAsync` when the reply contains a valid `TraceId`. Score name is `"M3-{scenario.Id}"`, value is 1 (Pass) or 0 (Fail).
- **Expose Langfuse keys in CI PromptDefense step**: Append an `env:` block to the PromptDefense execution step with `FDE_LANGFUSE_PUBLIC_KEY`, `FDE_LANGFUSE_SECRET_KEY`, `FDE_PARTICIPANT_ID`, `FDE_POD_ID`, and `FDE_EVENT_ID`.
- **Add post-deploy verification task to CD**: Append a task stage after the Container App deploy step that reads the live endpoint URL via `az containerapp show`, then runs `dotnet run -- milestone3 --url "$APP_URL"` with all required secrets in the pipeline context.

## Capabilities

### New Capabilities

- `langfuse-tracing`: Structured Langfuse score posting with trace-level identity, per-scenario score granularity, environment tagging, and metadata-driven telemetry — replacing polling-based session resolution and text-packing comment conventions.

### Modified Capabilities

<!-- No existing specs to modify — openspec/specs/ is empty. -->

## Impact

- **`eval/EvalRunner/LangfuseScoreClient.cs`**: `PostScoreAsync` signature changes (adds `comment` param, adds `environment` + `metadata` to payload, removes session-id polling). The `TryResolveSessionIdAsync` method is removed entirely.
- **`eval/EvalRunner/EvalRuntime.cs`**: `TryPostScoreAsync` gains optional `comment` parameter; internal call chain loses `TryResolveSessionIdAsync` dependency.
- **`eval/EvalRunner/Milestone3Command.cs`**: Scoring calls injected inside the scenario loop, using per-scenario trace IDs and dynamic score names.
- **`.github/workflows/ci.yml`**: PromptDefense step gains `env:` block exposing Langfuse keys and participant identity.
- **`.github/workflows/cd.yml`**: Post-deploy verification step added with `az containerapp show` + `dotnet run milestone3` with secrets piping.