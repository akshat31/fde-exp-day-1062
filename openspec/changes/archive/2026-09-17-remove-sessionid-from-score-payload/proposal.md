## Why

The Langfuse v1 Scores API (`POST /api/public/scores`) requires mutually exclusive linking fields: `traceId`, `sessionId`, or `datasetRunId`. The previous change `use-langfuse-v4-native-fields` added both `traceId` and `sessionId` to the payload, which the API rejects with HTTP 400: `"Provide exactly one of the following: traceId (with optional observationId), sessionId or datasetRunId."` All score posts now fail.

## What Changes

- Remove `sessionId` from the score payload — it conflicts with `traceId` (v1 API requires exactly one link target)
- Keep `userId` — it is a non-linking attribute that does not conflict with `traceId`
- Keep `metadata.participant_id` for identity-driven filtering via the Langfuse metadata index
- No changes to `EvalRuntime` or caller code (signature stays same)

## Capabilities

### Modified Capabilities
- `langfuse-tracing`: Score payload SHALL remove `sessionId` (conflicts with `traceId`) and keep `userId` (non-linking attribute)

## Impact

- `eval/EvalRunner/LangfuseScoreClient.cs` — remove `sessionId = participantId` from each anonymous payload branch