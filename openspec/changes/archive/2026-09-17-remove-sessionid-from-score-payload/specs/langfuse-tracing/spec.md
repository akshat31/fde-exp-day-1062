## MODIFIED Requirements

### Requirement: Score client posts directly to trace without session ID, with user identity

The score posting system SHALL map each score directly to a trace ID using the Langfuse Scores API without polling the trace API for session ID resolution.

The posted score payload SHALL include:
- The `traceId` provided by the caller
- A `userId` field set to the participant identifier (e.g. `"FDE-1000"`) — a non-linking attribute that does not conflict with `traceId`
- An `environment` field set to `"ci-cd-pipeline"`
- A `metadata` JSON sub-object containing `participant_id`, `pod_id`, and `event_id`
- The score `name`, numeric `value`, and `dataType` of `"NUMERIC"`

The system SHALL NOT include `sessionId` in the payload — the v1 API requires exactly one link target (`traceId`, `sessionId`, or `datasetRunId`), and `sessionId` is mutually exclusive with `traceId`.

The system SHALL NOT fabricate session IDs or poll the trace API to resolve them.

#### Scenario: Score posted with trace ID, user ID, and structured metadata

- **WHEN** a caller invokes score posting with a valid trace ID, score name, and numeric value
- **THEN** the system POSTs a payload to the Langfuse Scores API containing `traceId`, `userId` set to the participant identifier, `environment` set to `"ci-cd-pipeline"`, and a `metadata` object with `participant_id`, `pod_id`, and `event_id`

#### Scenario: Score API accepts payload without error

- **WHEN** the system POSTs a score payload to the Langfuse Scores API
- **THEN** the API returns HTTP 2xx (not 400), confirming the payload satisfies the mutual-exclusivity constraint

#### Scenario: User ID is populated on score for filtering

- **WHEN** an organizer views the Langfuse score list
- **THEN** the `userId` column is populated with the participant identifier, enabling per-participant filtering

#### Scenario: Missing trace ID fails fast

- **WHEN** a caller invokes score posting with a null or whitespace trace ID
- **THEN** the system throws `InvalidOperationException` and does not fabricate a session identifier