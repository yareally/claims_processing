# Answers: code review drill

To see these fail, paste the flawed method into `ClaimSubmissionWorkflow.cs` on this branch and run `dotnet test`. Each problem below fails at least one test.

| # | The problem | What goes wrong for the business | The fix | A test that catches it |
|---|---|---|---|---|
| 1 | The result of `GetOrAddAsync` is used, but the code never checks whether the claim already existed | A retry by the client reserves the money twice and opens a second case | Compare claim IDs and return early for a duplicate | `The_same_key_returns_the_same_claim_and_runs_no_step_again` |
| 2 | The email address is written to the logs | Personal data spreads to every system that stores logs, which is a privacy and compliance problem | Log the claim ID only | `The_email_address_never_appears_in_the_logs` |
| 3 | A rejected claim is returned but never saved | `GET /claims/{id}` shows `InProgress` forever, so nobody can see that the claim was rejected | Save before returning | `A_policy_without_coverage_rejects_the_claim_and_calls_nothing_else` |
| 4 | The coverage check is outside the `try` | If the policy service is down, the caller gets an unhandled 500 and the claim stays `InProgress` forever | Move the check inside the `try` | `A_failed_step_compensates_the_completed_steps_newest_first` |
| 5 | The email is inside the `try` | A failed email undoes a valid claim, and the customer loses it because of a mail problem | Send the email after the required steps, with its own `try` | `A_failed_notification_keeps_the_claim_submitted` |
| 6 | Progress is saved only at the end | After a crash, nothing records the reserve ID, so nobody can find the money to release | Save after each step that creates something | `Progress_is_saved_before_the_next_step_starts` |
| 7 | Compensation runs oldest first | The reserve is released while its case is still open. Later steps often depend on earlier ones, so undo them in reverse | Use a stack, or walk the steps backward | `A_failed_adjuster_step_undoes_the_case_then_the_reserve` (end-to-end) |
| 8 | Compensation and the final save use the caller's token | A customer closing the browser cancels the undo, and the money stays reserved with no record | Use `CancellationToken.None` for both | `When_the_caller_cancels_the_completed_steps_are_still_compensated` |
| 9 | Compensation has no retry and no `try`, so the first failure stops the rest | A brief network error leaves the case open and the claim stuck as `InProgress`, and `CompensationFailed` is never set, so nobody learns of it | Retry each one, catch each failure, record it, and continue | `One_failed_compensation_does_not_stop_the_others` |
| 10 | `FailedStep` is never set, and the log has only `ex.Message` with no claim ID | Support cannot tell which claim failed, at which step, or why, because the stack trace is gone | Set `FailedStep`, and log with `_logger.LogError(exception, "... {ClaimId} ...", claim.ClaimId)` | `A_compensated_claim_returns_503_and_names_the_failed_step` |

## What this drill shows

The draft compiles, and all three `Success_` tests in `Level1CoreWorkflowTests` pass against it. Most of its bugs show up only when something fails, which is where AI-written drafts need the closest review. Ask the AI, or yourself, what happens at each line if the previous call threw an exception or the caller left.
