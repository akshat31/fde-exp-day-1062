## 1. Update LangfuseScoreClient — Remove session polling, add metadata and environment

- [x] 1.1 Remove `TryResolveSessionIdAsync` method (lines 105-142) from `eval/EvalRunner/LangfuseScoreClient.cs`. Verify the method no longer exists in the file.
- [x] 1.2 Remove the `sessionId` resolution call on line 62 and the session-linked payload branch (lines 64-72). Replace with a single payload that uses `traceId` directly. Verify the method body has exactly one payload construction and no session-related code.
- [x] 1.3 Add `string? comment = null` parameter to `PostScoreAsync` method signature. Verify compilation succeeds with existing callers that omit the parameter.
- [x] 1.4 Add `environment = "ci-cd-pipeline"` field to the score payload anonymous object. Verify the payload serialization includes the `environment` key.
- [x] 1.5 Add `metadata` anonymous sub-object to the payload with `participant_id`, `pod_id`, and `event_id`. Remove the old packed `comment` string. Verify the payload includes a `metadata` object and no packed identity string.
- [x] 1.6 Conditionally include the `comment` field in the payload only when `comment` is not null. Verify omitting it produces a payload without `comment`, providing one includes it.
- [x] 1.7 Change return type from `(string Link, bool ViaSession)` to `string Link`. Update return statement to return only `traceId`. Verify compilation and all consuming code.

## 2. Update EvalRuntime — Streamline TryPostScoreAsync signature

- [x] 2.1 Add optional `string? comment = null` parameter to `TryPostScoreAsync`. Verify default-parameter callers still compile.
- [x] 2.2 Forward the `comment` parameter through to `PostScoreAsync` call. Remove `ViaSession` from the destructured result (return type changed). Verify the logging message no longer references session vs. trace link type.

## 3. Inject per-scenario scores in M3 milestone loop

- [x] 3.1 Inside the `foreach (var scenario in BuildScenarios())` block of `RunAsync`, after each scenario result is recorded in `scenarioResults`, check if `reply?.TraceId` is non-null. If valid, call `EvalRuntime.TryPostScoreAsync(traceId, $"M3-{scenario.Id}", passed ? 1 : 0)`. Verify per-scenario scores appear in Langfuse dashboard with names like `"M3-S1"`, `"M3-S2"`.
- [x] 3.2 For error cases (where `error is not null` and `reply` is null), skip the per-scenario score call. Verify no score is posted for failed-request scenarios.

## 4. Inject env variables into CI PromptDefense step

- [x] 4.1 Add `env:` block to the PromptDefense grade execution step in `.github/workflows/ci.yml` exposing `FDE_LANGFUSE_PUBLIC_KEY`, `FDE_LANGFUSE_SECRET_KEY`, `FDE_PARTICIPANT_ID`, `FDE_POD_ID`, and `FDE_EVENT_ID` from secrets/vars. Verify the step's dotnet run process can read these environment variables.

## 5. Add post-deploy verification to CD pipeline

- [x] 5.1 Append a new step after the "Deploy to this participant's Container App" step in `.github/workflows/cd.yml` that runs `az containerapp show` to retrieve the live FQDN and stores it. Verify the step outputs the resolved URL.
- [x] 5.2 Add a subsequent step that runs `dotnet run --no-build --configuration Release --project eval/EvalRunner -- milestone3 --url "$APP_URL"` with `FDE_LANGFUSE_PUBLIC_KEY`, `FDE_LANGFUSE_SECRET_KEY`, `FDE_PARTICIPANT_ID`, `FDE_POD_ID`, `FDE_EVENT_ID` in the `env:` block. Verify M3 scores appear in Langfuse after a CD deployment.

## 6. Local validation

- [x] 6.1 Run `dotnet build --configuration Release` from the repository root. Verify zero compilation errors after all signature changes.
- [x] 6.2 Run a mock evaluation with `FDE_PARTICIPANT_ID=FDE-TEST FDE_POD_ID=local-validation-pod FDE_EVENT_ID=local-smoke-test dotnet run --project eval/EvalRunner -- milestone3 --url "http://localhost:8080"`. Verify the runner reports warnings (not crashes) when Langfuse endpoints are unreachable, and scores post successfully when keys are configured.
- [ ] 6.3 Trigger the CD pipeline on GitHub. Open the Langfuse dashboard and verify that Trace Name, Environment, and Metadata fields are populated and cleanly indexed for posted scores.