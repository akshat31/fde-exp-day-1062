## Why

Scores posted to Langfuse carry participant identity only inside `metadata.participant_id` — a JSON sub-object that the Langfuse dashboard does not surface as a filterable column. Organizers cannot filter by participant ID (e.g. FDE-1000) in the score view without resorting to API-level queries. The Langfuse v4 platform supports `sessionId` and `userId` natively in the scores payload, which are indexed and surfaced as first-class filters in the dashboard.

## What Changes

- Add `sessionId` to the score payload, set to `FDE_PARTICIPANT_ID` (e.g. `"FDE-1000"`), so scores appear grouped under a session column in the Langfuse dashboard — filterable by participant
- Add `userId` to the score payload, also set to `FDE_PARTICIPANT_ID`, for future user-level score analytics
- Keep `metadata` sub-object with `participant_id`, `pod_id`, and `event_id` for backward compatibility and programmatic queries
- Keep `traceId`-based direct posting (no polling) — the new `sessionId` and `userId` are native fields accepted by the v1 scores endpoint alongside `traceId`
- No signature changes to `PostScoreAsync` or `TryPostScoreAsync` — the participant ID is already available as a parameter

## Capabilities

### Modified Capabilities
- `langfuse-tracing`: Score payload SHALL include `sessionId` and `userId` native fields

## Impact

- `eval/EvalRunner/LangfuseScoreClient.cs` — payload construction