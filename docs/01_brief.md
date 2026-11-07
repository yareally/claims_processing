# The brief

## What you need to do

Finish two methods. Everything else is already written, including the tests, which check your code.

| File | What to write |
|---|---|
| `src/Claims.Api/Workflow/ClaimSubmissionWorkflow.cs` | The method `SubmitAsync`, plus any private helper methods it needs |
| `src/Claims.Api/ClaimEndpoints.cs` | The method `ToHttpResult`, which turns a claim status into an HTTP response |

| Level | Code to write | Tests | Command |
|---|---|---|---|
| 1 | Call the steps in order, stop if the policy doesn't cover the claim, catch failures, undo finished steps newest first, and save the final claim. Write `ToHttpResult`. | 15 | `dotnet test --filter Level1` |
| 2 | Retry each undo, continue when one still fails, ignore the caller's cancellation during undo, save after each step, and keep the claim when the email fails. | 7 | `dotnet test --filter Level2` |
| 3 | Return the existing claim for a repeated `Idempotency-Key`. Keep the email address out of the logs. | 5 | `dotnet test --filter Level3` |

**Errors and exceptions:** read "How failures reach your code" below before you start. It lists the exceptions the services throw and where your code must catch them.

**Already written, so you only call it:** the five service interfaces and their HTTP clients, `InMemoryClaimStore`, `ClaimRecord` and its statuses, `WorkflowOptions`, the API endpoints, the request validation, and the stub services.

Do not change the public method signatures, because the tests depend on them. The numbered acceptance criteria below give the details.

## User story

As a policyholder, I want to submit an insurance claim online, so that money is set aside for it, a case is opened, and an adjuster starts work.

## How the work splits across services

No single database transaction can cover five separate services. Each service commits its own work. If a later step fails, your code must undo the earlier steps by calling each service again. That undo call is a **compensation**. This design is known as the **saga pattern**.

| Order | Service method | What it does | How to undo it |
|---|---|---|---|
| 1 | `IPolicyService.CheckCoverageAsync` | Checks that the policy covers the claim | Nothing to undo: it only reads |
| 2 | `IReserveService.CreateReserveAsync` | Sets money aside | `ReleaseReserveAsync` |
| 3 | `ICaseService.OpenCaseAsync` | Opens a case | `CancelCaseAsync` |
| 4 | `IAdjusterService.AssignAdjusterAsync` | Assigns an adjuster | Nothing to call: cancelling the case frees the adjuster |
| 5 | `INotificationService.SendClaimReceivedAsync` | Emails the claimant | Cannot be undone |

Steps 1 to 4 are required. Step 5 is not: see item 10.

## How failures reach your code

`SubmitAsync` never throws. Every outcome, failures included, ends with `SubmitAsync` returning a claim that has a status.

A service reports a failure by throwing an exception:

| What happens | What your code sees | What to do |
|---|---|---|
| A service returns an error, times out, or can't be reached | The call throws `DownstreamServiceException` | Treat the step as failed |
| The caller disconnects | The next call that gets `cancellationToken` throws `OperationCanceledException` | Treat the step as failed |
| A bug, or a failed save | Any other exception | Treat the step as failed |
| The policy doesn't cover the claim | `CheckCoverageAsync` returns `IsCovered = false`, with no exception | Reject the claim (item 2) |

So catch `Exception`, not only `DownstreamServiceException`. You need three kinds of `try`/`catch`:

1. **One around steps 1 to 4.** After each step that creates something, record how to undo it before you save. Then a failed save still undoes that step. In the `catch`, log the exception, set `FailedStep` to the step that was running, and undo the recorded steps newest first (item 3).
2. **One around each undo call.** Catch, log, and try again (item 6). After the last attempt, add a `CompensationFailure` and go on to the next undo (item 7).
3. **One around the email (step 5).** Catch, log, and return the `Submitted` claim with `NotificationSent` false (item 10).

Pass `CancellationToken.None` to every undo call and every final save, so a disconnected caller cannot stop them (item 8).

The shape of the first one:

```csharp
try
{
    // Steps 1 to 4. After each step that creates something, record its undo, then save.
}
catch (Exception exception)
{
    // Log the exception, then undo the recorded steps, newest first.
}
```

## Acceptance criteria

### Level 1: the core workflow

1. **Success.** Run the steps in the order above. Finish with the status `Submitted`, the three service IDs on the record, and `NotificationSent` set to true.
2. **No coverage.** If the policy does not cover the claim, set the status to `Rejected` and copy the reason to `RejectionReason`. Call no other service.
3. **Failure.** If a required step throws an exception, compensate every completed step, newest first. Do not compensate the step that failed. Set the status to `Compensated` and set `FailedStep`.
4. **Saving.** Save the final record for every outcome, before you return it.
5. **Status codes.** `ToHttpResult` returns these codes, with `ClaimResponse.From(claim)` as the body:

   | Claim status | HTTP status | Why |
   |---|---|---|
   | `Submitted` | 201, with the header `Location: /claims/{claimId}` | The claim now exists |
   | `Rejected` | 422 | The request was valid, but the policy does not cover it |
   | `InProgress` | 409 | An earlier request with this key is still running |
   | `Compensated` | 503 | A service failed and nothing was kept |
   | `CompensationFailed` | 500 | A service failed and something could not be undone |

### Level 2: when things go wrong

6. **Retry.** Try each compensation up to `WorkflowOptions.CompensationAttempts` times in total. Wait `CompensationRetryDelay` between tries.
7. **Keep going.** A compensation that still fails must not stop the others. Set the status to `CompensationFailed` and add a `CompensationFailure` to `CompensationFailures`, naming the step and the resource ID, so a person can repair it.
8. **Cancelled requests.** The caller can disconnect at any time, which cancels `cancellationToken`. Completed steps must still be compensated, and the final record must still be saved.
9. **Progress.** After each step that creates something, save the record before the next step starts. If the process crashes, the saved IDs show what needs undoing.
10. **Email failures.** Once step 4 succeeds, the claim is accepted. If the email then fails, keep the status `Submitted`, leave `NotificationSent` false, and compensate nothing.

### Level 3: duplicates and logs

11. **Duplicates.** Clients retry when a network call fails, so the same request can arrive twice. The `Idempotency-Key` header identifies a request. If the key has a claim already, return that claim and run no steps, even when the first request is still running. Use `IClaimStore.GetOrAddAsync`.
12. **Logs.** Log enough for support to find a failed claim, including the claim ID and the failed step. Never log the email address.

## Hints

Read these only if you are stuck.

- `ClaimRecord` cannot be changed in place. Write `claim = claim with { ReserveId = id };`.
- Think about the data structure that returns the newest item first.
- `CancellationToken.None` is a token that never cancels.
