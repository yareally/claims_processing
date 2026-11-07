# The walkthrough

The assessment ends with a brief spoken explanation. Practise aloud and record yourself. It feels different from knowing the answer.

## Your 3-minute explanation

Use this order:

1. **The problem (20 seconds).** What the workflow does, and why one database transaction cannot cover it.
2. **The design (60 seconds).** How the steps run, how compensation works, and why the email comes last.
3. **Failure cases (60 seconds).** Pick the two you handled most carefully, and say why they matter to the business.
4. **Tools (20 seconds).** What you looked up or asked the AI tool, and how you checked the answer.
5. **What you would do next (20 seconds).** What this code still lacks for production.

## Likely follow-up questions

Answer each one aloud in 30 seconds. The answers are on the `solution` branch.

1. Why not use one distributed transaction across all the services?
2. You used one coordinator. What is the other way to build a saga, and when would you choose it?
3. The call to create a reserve times out. You do not know whether the reserve exists. What now?
4. The process crashes between steps 2 and 3. What happens to the reserved money?
5. A compensation keeps failing. What should happen next?
6. Why is the email last?
7. Should the required steps be retried as well?
8. You run three copies of this API. What breaks in this code?
9. How would you run this on AWS?
10. The same `Idempotency-Key` arrives with a different claim amount. What should happen?
11. How would you find out why a claim failed in production?

## Score yourself

Give yourself one point for each.

- [ ] I named the pattern (saga) and explained it in plain words.
- [ ] I said why compensation runs newest first.
- [ ] I said why compensation does not use the caller's cancellation token.
- [ ] I explained why repeating a compensation must be safe.
- [ ] I explained the difference between `Compensated` and `CompensationFailed`, and what a person does about the second.
- [ ] I explained why an email failure does not undo the claim.
- [ ] I explained how the `Idempotency-Key` header stops duplicate work.
- [ ] I named at least two gaps before production, such as a durable store and a recovery job.
- [ ] I gave one specific example of what I looked up or asked, and how I checked it.
- [ ] I finished within 3 minutes without reading from notes.
