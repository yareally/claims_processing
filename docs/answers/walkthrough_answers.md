# Answers: walkthrough follow-up questions

These are short answers to say aloud. Several name gaps in the reference solution. Naming a gap and how you would close it is a strong answer.

**1. Why not one distributed transaction across all the services?**
Two-phase commit needs every service and database to support it, and these services only offer HTTP. It also holds locks across all of them until the slowest finishes, so one slow service delays the rest. A saga accepts that the system is briefly inconsistent and repairs it with compensation.

**2. What is the other way to build a saga?**
Choreography: each service publishes an event, and the next service reacts, with no coordinator. It suits a few steps owned by separate teams, but the overall flow is harder to see and debug. I used a coordinator (orchestration) because the order is strict and all the failure handling sits in one place.

**3. The reserve call times out. You do not know whether the reserve exists.**
This is a gap in the reference solution: it treats the step as not done, so a reserve could be left open. To fix it, make creation safe to repeat by using the claim ID as the key, for example `PUT /reserves/{claimId}`. Then compensate by claim ID even when the result is unknown, and treat a 404 as success.

**4. The process crashes between steps 2 and 3.**
The money stays reserved, and the saved record shows `InProgress` with a `ReserveId`. That saved state is why item 9 matters. The code still needs a recovery job that finds old `InProgress` claims and compensates them from the saved IDs. A workflow service such as AWS Step Functions keeps this state for you.

**5. A compensation keeps failing.**
After the retries, the claim becomes `CompensationFailed`, and an error log names the claim, step, and resource. In production I would alert on that log, keep retrying from a queue with longer waits, and move it to a dead-letter queue (a holding queue for messages that keep failing) for a person to repair. Support needs a written procedure, because money stays held until then.

**6. Why is the email last?**
An email cannot be recalled, so it runs after every step that can still fail. Otherwise a customer could get "claim received" for a claim that was then undone. Its failure also does not change the business result, so `NotificationSent` stays false and a later job can send it again.

**7. Should the required steps be retried as well?**
Only when repeating the call is safe. A coverage check only reads, so it is safe. A create call needs an idempotency key, or a retry could reserve the money twice. Retry only temporary failures such as timeouts, 503, and 429, and wait longer each time with some random variation. In .NET, `Microsoft.Extensions.Http.Resilience` provides this. The brief did not ask for it, so I left it out.

**8. You run three copies of this API. What breaks?**
`InMemoryClaimStore` lives in one process. Duplicates would not be detected across copies, `GET /claims/{id}` could return 404 from the wrong copy, and a restart loses everything. Use a shared database, with a unique index on the idempotency key in SQL, or a conditional write in DynamoDB. That keeps the check-and-add atomic.

**9. How would you run this on AWS?**
Run the API on ECS Fargate or Lambda, behind an Application Load Balancer or API Gateway. Keep claim state in DynamoDB or RDS. AWS Step Functions could replace the coordinator: each task has a `Catch` that routes to its compensation states, and it records progress for you. Use SQS with a dead-letter queue for failed compensations, CloudWatch for logs and alarms, and X-Ray for tracing.

**10. The same key arrives with a different amount.**
This is another gap: the code returns the first claim and says nothing. The usual fix stores a hash of the request with the key and returns 422 if they differ. Keys should also belong to one caller, so one user cannot read another's claim by guessing a key, and they should expire, for example after 24 hours.

**11. How would you find out why a claim failed in production?**
Start with `GET /claims/{id}`, which shows `FailedStep`. Then search the logs by claim ID: the structured field `{ClaimId}` makes them searchable, and they hold the exception and stack trace. Pass a trace ID between services, using OpenTelemetry or X-Ray, to follow the request across them. Add a metric for failures by step, and an alert on `CompensationFailed`.

## Other production gaps to mention

- Authentication: nothing checks who the caller is, or that they own the policy.
- Compensation also stops if the process shuts down during it. Handle this with the recovery job from question 4.
- The final save in compensation can itself fail, which returns a 500 and leaves the claim `InProgress`. The recovery job covers this too.
- `CancellationToken.None` removes the caller's limit, so `CompensationAttempts`, the retry delay, and the client timeout together set the longest possible wait.
