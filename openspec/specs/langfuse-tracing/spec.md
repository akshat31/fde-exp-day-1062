## Purpose

Structured telemetry integration that posts evaluation scores to Langfuse with trace-level identity, per-scenario granularity, environment tagging, and metadata-driven observability — enabling organizers to query and correlate evaluation results across participants, pods, and events directly in the Langfuse dashboard.

## ADDED Requirements

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

### Requirement: Optional comment field in score posting

The score posting pipeline SHALL accept an optional `comment` string parameter that is forwarded to the Langfuse Scores API payload as a `comment` field, in addition to the structured `metadata` sub-object.

#### Scenario: Score posted with comment

- **WHEN** a caller provides a non-null comment string
- **THEN** the posted payload includes the `comment` field alongside the `metadata` object

#### Scenario: Score posted without comment

- **WHEN** a caller omits the comment parameter (null)
- **THEN** the posted payload omits the `comment` field entirely

### Requirement: Per-scenario scoring in milestone evaluation

During adversarial scenario evaluation, the system SHALL post an individual score to Langfuse for each scenario that returns a valid trace ID in its agent reply.

Each per-scenario score SHALL:
- Use score name `"M3-{scenario.Id}"` (e.g., `"M3-S1"`, `"M3-S2"`)
- Use value 1 for a passing scenario and 0 for a failing scenario
- Use the trace ID from the agent reply as the score's link target

#### Scenario: Passing scenario posts score of 1

- **WHEN** scenario S1 evaluation completes with a Pass result and a valid trace ID in the agent reply
- **THEN** the system posts a score named `"M3-S1"` with value 1 linked to that trace ID

#### Scenario: Failing scenario posts score of 0

- **WHEN** scenario S3 evaluation completes with a Fail result and a valid trace ID in the agent reply
- **THEN** the system posts a score named `"M3-S3"` with value 0 linked to that trace ID

#### Scenario: Scenario without trace ID skips per-scenario score

- **WHEN** scenario evaluation completes but the agent reply has no trace ID (e.g., the request failed with an error)
- **THEN** the system SHALL NOT post a per-scenario score for that iteration

### Requirement: CI PromptDefense step exposes Langfuse environment variables

The CI workflow's PromptDefense execution step SHALL expose `FDE_LANGFUSE_PUBLIC_KEY`, `FDE_LANGFUSE_SECRET_KEY`, `FDE_PARTICIPANT_ID`, `FDE_POD_ID`, and `FDE_EVENT_ID` as environment variables to the runner process.

#### Scenario: PromptDefense step runs with Langfuse credentials present

- **WHEN** the CI workflow executes the PromptDefense grade step
- **THEN** the `FDE_LANGFUSE_PUBLIC_KEY`, `FDE_LANGFUSE_SECRET_KEY`, `FDE_PARTICIPANT_ID`, `FDE_POD_ID`, and `FDE_EVENT_ID` environment variables are available to the dotnet process

### Requirement: CD pipeline includes post-deploy evaluation verification

The CD workflow SHALL include a post-deployment verification step that, after the Container App deploy step completes, reads the live endpoint URL from Azure and triggers the M3 evaluation runner against that URL with all required secrets available as environment variables.

#### Scenario: Post-deploy verification executes M3 against live endpoint

- **WHEN** the CD workflow completes the Container App deploy step
- **THEN** the system retrieves the live container endpoint FQDN via `az containerapp show` and executes `dotnet run -- milestone3 --url "<resolved URL>"` with `FDE_LANGFUSE_PUBLIC_KEY`, `FDE_LANGFUSE_SECRET_KEY`, `FDE_PARTICIPANT_ID`, `FDE_POD_ID`, and `FDE_EVENT_ID` in the environment