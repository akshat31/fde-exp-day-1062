## Context

Scores are posted to the Langfuse v1 Scores API at `/api/public/scores` with Basic Auth. The current payload carries identity only in `metadata.participant_id` — a JSON sub-object not indexed as a dashboard column. The Langfuse v4 platform's v1 scores endpoint accepts `sessionId` and `userId` as native fields alongside `traceId`. These fields are indexed and surfaced as first-class filter columns in the dashboard score view.

The calling path is `EvalRuntime.TryPostScoreAsync` → `LangfuseScoreClient.PostScoreAsync`. `EvalRuntime` already reads `FDE_PARTICIPANT_ID` from the environment and passes it through as `participantId`. See proposal.md for motivation.

## Goals / Non-Goals

**Goals:**
- Add `sessionId` and `userId` to the score payload using the participant identifier already flowing through the call chain
- Keep the existing `metadata` sub-object intact for backward compatibility
- Surface session-based filtering in the Langfuse dashboard score view

**Non-Goals:**
- Migrating from v1 to v3 scores endpoint (the v1 endpoint already supports these fields in v4 platform)
- Changing the `PostScoreAsync` or `TryPostScoreAsync` signatures
- Polling traces to resolve session IDs (the participant identifier IS the session)
- Adding per-eval-run session creation (one session per participant is sufficient)

## Decisions

### Decision 1: Use participant ID as both sessionId and userId

**Rationale:** Each participant (FDE-1000, FDE-1001, etc.) runs evaluations independently through CI/CD pipelines. Their scores naturally form a session — all scores from FDE-1000's runs belong together. Using the participant identifier means the Langfuse dashboard's session filter becomes a participant filter with zero additional configuration. The `userId` parallel value enables user-level score analytics if the platform adds user-scoped views later.

**Alternatives considered:**
- **Generate a run-specific session UUID per CI run:** Would create many sessions per participant (one per pipeline run), making "show me all FDE-1000 scores" harder — organizer would need to filter across dozens of sessions
- **Use pod_id as sessionId:** Would group by pod instead of participant; less useful for organizer-level view
- **Poll trace API for `langfuse.session.id`:** Reintroduces latency the previous change removed; adds network dependency for a value the eval runner already knows

### Decision 2: Keep metadata sub-object unchanged

**Rationale:** Organizers may already have scripts or dashboards querying `metadata.participant_id` via the Langfuse API. Removing it would be a breaking change for those consumers. The `metadata` field and native `sessionId`/`userId` fields coexist without conflict — the score endpoint accepts all of them.

**Alternatives considered:**
- **Remove metadata, rely only on sessionId/userId:** Cleaner but breaks existing API consumers; metadata is indexable in Langfuse while sessionId is only present in score-linking context
- **Move pod_id and event_id to separate native fields:** No native fields exist for these dimensions in the v1 API

### Decision 3: No signature changes

**Rationale:** `PostScoreAsync` already receives `participantId` as a parameter. The payload construction inside the method body does not need any new parameters — just two additional anonymous object properties. All callers remain unchanged.

## Risks / Trade-offs

- **sessionId collision risk:** Multiple CI runs from the same participant share a session. The Langfuse dashboard shows scores from different runs interleaved under the same session. → Mitigation: CI timestamps on each score distinguish runs; the score `name` field already encodes the milestone and scenario
- **userId may not be indexed as a filter column:** While `sessionId` is documented as a native score field with dashboard visibility, `userId` support in the score view is less certain. → Mitigation: `userId` is a forward-looking field; if it is not yet visible in the UI, `sessionId` alone provides the participant filter and `userId` becomes available when the platform adds user-scoped views
- **No session entity is created:** Scores reference a `sessionId` that has no corresponding session entity in Langfuse (sessions are created by traces carrying `langfuse.session.id`). → Mitigation: Langfuse v4 surfaces `sessionId` in the score view regardless of whether a session entity exists; this is confirmed by the documented session-level score ingestion pattern