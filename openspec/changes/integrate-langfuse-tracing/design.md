## Context

The eval runner posts scores to Langfuse using the legacy v1 Scores API at `/api/public/scores` (v2/v3 POST return 405 on the deployed instance). The current flow: `EvalRuntime.TryPostScoreAsync` → `LangfuseScoreClient.PostScoreAsync` → polls `GET /api/public/traces/{traceId}` up to 5 times (2s delay each) to resolve a `sessionId`, then posts the score keyed to that session or falls back to `traceId`. Participant/pod/event tags are packed into the `comment` field as `participant_id=X; pod_id=Y; event_id=Z` — a text string that is not queryable as structured data in the Langfuse dashboard.

Per-scenario adversarial scores (S1-S7 in M3) are collected only in the in-memory `scenarioResults` list. Only a single aggregate `"M3"` summary score is posted, keyed to whichever trace ID was last available. This means a failing S1 with S2-S7 passing is indistinguishable from a failing S7 with S1-S6 passing on the dashboard.

The CI workflow's PromptDefense step has no `env:` block exposing Langfuse keys, so PromptDefense scores never reach Langfuse. The CD workflow posts M2/M3 summary scores but has no post-deploy verification step that independently runs M3 as a separate task stage.

## Goals / Non-Goals

**Goals:**
- Eliminate the blocking 5-cycle polling loop from `PostScoreAsync` — scores post immediately on the caller's trace ID
- Structure identity metadata as a JSON sub-object rather than packed text in `comment`
- Post per-scenario scores (S1-S7) individually during M3 evaluation
- Add explicit `env:` blocks to CI and CD for Langfuse key propagation
- Add a post-deploy verification task stage to CD

**Non-Goals:**
- Migrating from the v1 Scores API to v2/v3 (the instance does not support POST on those paths)
- Adding Langfuse score posting to Milestone 2 per-step granularity (M2 already posts a summary score only)
- Changing the Langfuse authentication model (Basic Auth remains)
- Adding OTLP trace ingestion to the eval runner itself (traces come from the deployed BankingApp, not the runner)
- Modifying the `AgentClient` or the `/chat` contract

## Decisions

### Decision 1: Drop session polling, map directly to traceId

**Rationale:** The 5-cycle polling loop adds up to 10s of wall-clock latency per score with no observable benefit — the Langfuse UI resolves trace-linked and session-linked scores identically in the score list. The only difference is a session chip on the trace detail view, which is irrelevant for CI/CD pipeline scores where each trace is a discrete eval probe. Removing the polling simplifies the client, eliminates a class of transient network errors, and makes score posting deterministic.

**Alternatives considered:**
- **Keep polling with reduced attempts (1-2 cycles):** Still adds latency; the UI benefit is marginal.
- **Make polling async/fire-and-forget:** Complexity not warranted for CI scores.

### Decision 2: Use `metadata` JSON object instead of packed `comment` field

**Rationale:** Langfuse's v1 Scores API accepts a `metadata` field (JSON object) that is indexed and queryable in the dashboard. The current `comment` field is plain text — organizers cannot filter scores by `participant_id` without string-matching. Structured `metadata` enables dashboard filtering while keeping the `comment` field available for free-text annotations.

**Alternatives considered:**
- **Keep `comment` only, use structured text format:** Still not queryable; no filtering possible.
- **Use `metadata` only, drop `comment`:** Would lose the free-text annotation capability for ad-hoc notes.

### Decision 3: Optional `comment` parameter on `TryPostScoreAsync`

**Rationale:** The parameter is optional (`string? comment = null`) and forwarded through to `LangfuseScoreClient.PostScoreAsync`. When not provided, the payload omits the `comment` field entirely (not set to empty string). The structured identity tags live exclusively in `metadata`, so `comment` is purely for human-readable diagnostics. This is a non-breaking signature change — all existing callers that don't pass `comment` continue to work.

**Alternatives considered:**
- **Overload method:** Adds method-count clutter for one optional parameter.
- **Remove `comment` entirely:** Loss of ad-hoc annotation capability.

### Decision 4: Post per-scenario scores inside the `foreach` loop, not after

**Rationale:** Each adversarial scenario generates its own trace ID via `AgentClient.AskAsync`. Posting the score immediately inside the loop iteration is the only way to link that score to the correct trace — by the time the loop exits, earlier trace IDs are overwritten by `lastScenarioTraceId`. The score name format `"M3-{scenario.Id}"` (e.g., `"M3-S1"`) is designed for dashboard grouping: all M3 per-scenario scores share the `"M3-"` prefix and can be filtered or aggregated.

**Alternatives considered:**
- **Collect trace IDs + results, post after loop:** More code, same outcome; immediate posting is simpler and provides partial results even if a later scenario crashes.
- **Post per-scenario scores AND keep the aggregate M3 summary:** Both can coexist — the summary score provides a single pass/fail signal while per-scenario scores provide granular diagnostics. The aggregate score is not removed.

### Decision 5: CD post-deploy verification as a separate task step (not inline)

**Rationale:** The current CD workflow runs M2 and M3 eval steps inline after the deploy. Adding a separate task step with its own `az containerapp show` call provides an independent verification that the deployment completed and the app is reachable. This is not redundant with the inline eval steps — a failed `az containerapp update` could leave the previous revision running, and the inline evals would test the old revision if the health check passed on it. The separate step forces a fresh endpoint resolution.

**Alternatives considered:**
- **Add verification to the existing M3 step:** Would not catch stale-revision scenarios.
- **Use `az containerapp update --query` output:** The `--yaml` path does not return the resolved FQDN.

## Risks / Trade-offs

- **Loss of session-linked scores in Langfuse UI:** Scores posted directly to `traceId` lack the session chip on the trace detail view. Mitigation: CI/CD eval traces are short-lived probe requests with no session context; the session chip has no value here. Per-scenario scores are individually traceable by trace ID.
- **`metadata` field may not be indexed in older Langfuse versions:** The deployed instance is assumed to support v1 Scores API with `metadata`. If the field is silently dropped, identity data must revert to `comment`. Mitigation: the `metadata` and `comment` fields are independent in the payload — if `metadata` is unsupported, the score still posts with `comment` intact (from caller-provided comment or empty).
- **Per-scenario score volume:** 6-7 additional API calls per M3 run. Mitigation: the Langfuse Scores API is lightweight (one POST per score); 7 extra calls at ~50ms each add negligible overhead compared to the eval probe requests themselves.
- **CI PromptDefense env block may expose secrets in step logs:** GitHub Actions masks secret values in logs by default. Mitigation: `FDE_LANGFUSE_PUBLIC_KEY` and `FDE_LANGFUSE_SECRET_KEY` are configured as repository secrets, which GitHub automatically redacts from log output.