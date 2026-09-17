## 1. Update LangfuseScoreClient payload

- [x] 1.1 Add `sessionId = participantId` and `userId = participantId` properties to the anonymous payload objects in `PostScoreAsync` (both the `comment is not null` and `comment is null` branches). Verify JSON serialization output includes both fields with the expected values.

## 2. Verification

- [x] 2.1 Run `dotnet build --configuration Release` from the repository root. Verify zero compilation errors.
- [x] 2.2 Run a mock evaluation with `FDE_PARTICIPANT_ID=FDE-TEST FDE_POD_ID=local-validation-pod FDE_EVENT_ID=local-smoke-test dotnet run --project eval/EvalRunner -- milestone3 --url "http://localhost:8080"`. Verify the runner logs score post success and the Langfuse dashboard score view shows `sessionId` and `userId` columns populated with `FDE-TEST`.