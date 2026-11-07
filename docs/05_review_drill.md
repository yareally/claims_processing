# Code review drill

Do this after the timed run. Allow 10 minutes.

The job description asks for reviews that check generated code against "engineering, security, and quality standards." Imagine an AI tool wrote this method for you. It compiles, and it looks reasonable on a quick read.

**Find at least 7 problems.** There are 10. For each one, say what goes wrong for the business, not only what is wrong with the code. The answer key is `docs/answers/review_drill_answers.md` on the `solution` branch.

```csharp
public async Task<ClaimRecord> SubmitAsync(
    SubmitClaimRequest request,
    string idempotencyKey,
    CancellationToken cancellationToken)
{
    var claim = await _store.GetOrAddAsync(idempotencyKey, ClaimRecord.Start(request), cancellationToken);
    _logger.LogInformation("Processing claim for {Email}", request.ClaimantEmail);

    var coverage = await _policy.CheckCoverageAsync(request.PolicyNumber, request.LossDate, cancellationToken);
    if (!coverage.IsCovered)
    {
        return claim with { Status = ClaimStatus.Rejected, RejectionReason = coverage.Reason };
    }

    string? reserveId = null;
    string? caseId = null;
    try
    {
        reserveId = await _reserve.CreateReserveAsync(claim.ClaimId, claim.PolicyNumber, claim.EstimatedAmount, cancellationToken);
        caseId = await _cases.OpenCaseAsync(claim.ClaimId, claim.PolicyNumber, cancellationToken);
        var assignmentId = await _adjusters.AssignAdjusterAsync(caseId, cancellationToken);
        await _notifications.SendClaimReceivedAsync(claim.ClaimantEmail, claim.ClaimId, cancellationToken);

        claim = claim with
        {
            ReserveId = reserveId,
            CaseId = caseId,
            AssignmentId = assignmentId,
            NotificationSent = true,
            Status = ClaimStatus.Submitted,
        };
        await _store.SaveAsync(claim, cancellationToken);
        return claim;
    }
    catch (Exception ex)
    {
        _logger.LogError("Claim failed: {Message}", ex.Message);

        if (reserveId != null)
        {
            await _reserve.ReleaseReserveAsync(reserveId, cancellationToken);
        }

        if (caseId != null)
        {
            await _cases.CancelCaseAsync(caseId, cancellationToken);
        }

        claim = claim with { Status = ClaimStatus.Compensated };
        await _store.SaveAsync(claim, cancellationToken);
        return claim;
    }
}
```
