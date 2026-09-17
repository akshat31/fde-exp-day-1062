## 1. Remove sessionId and userId from score payload

- [x] 1.1 In `LangfuseScoreClient.PostScoreAsync`, remove `sessionId = participantId` from both anonymous payload branches (comment-present and comment-absent). Keep `userId = participantId` intact. Verify the payload JSON contains `userId` but not `sessionId`.

## 2. Build and functional verification

- [x] 2.1 Run `dotnet build --configuration Release` from the repository root. Verify zero compilation errors.
- [x] 2.2 Run M3 eval against the local BankingApp and verify the Langfuse score POST returns HTTP 2xx instead of 400.