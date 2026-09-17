## MODIFIED Requirements

### Requirement: Score client posts directly to trace with session and user identity

The score posting system SHALL map each score directly to a trace ID using the Langfuse Scores API without polling the trace API for session ID resolution.

The posted score payload SHALL include:
- The `traceId` provided by the caller
- A `sessionId` field set to the participant identifier (e.g. `"FDE-1000"`)
- A `userId` field set to the participant identifier (e.g. `"FDE-1000"`)
- An `environment` field set to `"ci-cd-pipeline"`
- A `metadata` JSON sub-object containing `participant_id`, `pod_id`, and `event_id`
- The score `name`, numeric `value`, and `dataType` of `"NUMERIC"`

The system SHALL NOT fabricate session IDs or poll the trace API to resolve them.

#### Scenario: Score posted with trace ID, session, user, and structured metadata

- **WHEN** a caller invokes score posting with a valid trace ID, score name, and numeric value
- **THEN** the system POSTs a payload to the Langfuse Scores API containing `traceId`, `sessionId` set to the caller's participant identifier, `userId` set to the same participant identifier, `environment` set to `"ci-cd-pipeline"`, and a `metadata` object with `participant_id`, `pod_id`, and `event_id`

#### Scenario: Score appears under session filter in Langfuse dashboard

- **WHEN** an organizer views the Langfuse score list with a participant identifier like `"FDE-1000"`
- **THEN** scores for that participant are filterable by the `sessionId` column in the score view, and the `sessionId` matches the participant identifier

#### Scenario: Missing trace ID fails fast

- **WHEN** a caller invokes score posting with a null or whitespace trace ID
- **THEN** the system throws `InvalidOperationException` and does not fabricate a session identifier