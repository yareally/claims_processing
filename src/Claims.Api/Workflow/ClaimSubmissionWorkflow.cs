using Claims.Api.Services;

namespace Claims.Api.Workflow;

/// <summary>
/// Coordinates the services that handle a new claim. This is the saga pattern with one
/// coordinator: each step commits on its own, and a later failure is repaired by running
/// the undo action (the compensation) of each completed step, newest first.
/// </summary>
public sealed class ClaimSubmissionWorkflow
{
    private readonly IPolicyService _policy;
    private readonly IReserveService _reserve;
    private readonly ICaseService _cases;
    private readonly IAdjusterService _adjusters;
    private readonly INotificationService _notifications;
    private readonly IClaimStore _store;
    private readonly WorkflowOptions _options;
    private readonly ILogger<ClaimSubmissionWorkflow> _logger;

    public ClaimSubmissionWorkflow(
        IPolicyService policy,
        IReserveService reserve,
        ICaseService cases,
        IAdjusterService adjusters,
        INotificationService notifications,
        IClaimStore store,
        WorkflowOptions options,
        ILogger<ClaimSubmissionWorkflow> logger)
    {
        _policy = policy;
        _reserve = reserve;
        _cases = cases;
        _adjusters = adjusters;
        _notifications = notifications;
        _store = store;
        _options = options;
        _logger = logger;
    }

    public async Task<ClaimRecord> SubmitAsync(
        SubmitClaimRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var newClaim = ClaimRecord.Start(request);
        var storedClaim = await _store.GetOrAddAsync(idempotencyKey, newClaim, cancellationToken);
        if (storedClaim.ClaimId != newClaim.ClaimId)
        {
            _logger.LogInformation("Duplicate request for claim {ClaimId}. Returning the stored claim.", storedClaim.ClaimId);
            return storedClaim;
        }

        var claim = await RunRequiredStepsAsync(newClaim, cancellationToken);
        return claim.Status == ClaimStatus.Submitted
            ? await NotifyClaimantAsync(claim, cancellationToken)
            : claim;
    }

    private async Task<ClaimRecord> RunRequiredStepsAsync(ClaimRecord claim, CancellationToken cancellationToken)
    {
        // A stack returns the newest compensation first, so a new step only has to push its own.
        var compensations = new Stack<Compensation>();
        var currentStep = WorkflowStep.CheckCoverage;
        try
        {
            var coverage = await _policy.CheckCoverageAsync(claim.PolicyNumber, claim.LossDate, cancellationToken);
            if (!coverage.IsCovered)
            {
                claim = claim with { Status = ClaimStatus.Rejected, RejectionReason = coverage.Reason };
                await _store.SaveAsync(claim, CancellationToken.None);
                return claim;
            }

            currentStep = WorkflowStep.CreateReserve;
            var reserveId = await _reserve.CreateReserveAsync(claim.ClaimId, claim.PolicyNumber, claim.EstimatedAmount, cancellationToken);
            compensations.Push(new Compensation(currentStep, reserveId, token => _reserve.ReleaseReserveAsync(reserveId, token)));
            claim = claim with { ReserveId = reserveId };
            await _store.SaveAsync(claim, cancellationToken);

            currentStep = WorkflowStep.OpenCase;
            var caseId = await _cases.OpenCaseAsync(claim.ClaimId, claim.PolicyNumber, cancellationToken);
            compensations.Push(new Compensation(currentStep, caseId, token => _cases.CancelCaseAsync(caseId, token)));
            claim = claim with { CaseId = caseId };
            await _store.SaveAsync(claim, cancellationToken);

            currentStep = WorkflowStep.AssignAdjuster;
            var assignmentId = await _adjusters.AssignAdjusterAsync(caseId, cancellationToken);
            claim = claim with { AssignmentId = assignmentId, Status = ClaimStatus.Submitted };

            // Every required step is done, so the caller leaving now must not undo the claim.
            await _store.SaveAsync(claim, CancellationToken.None);
            return claim;
        }
        catch (Exception exception)
        {
            // This catch is deliberately broad: whatever went wrong, the completed steps must be undone.
            _logger.LogError(exception, "Claim {ClaimId} failed at step {Step}.", claim.ClaimId, currentStep);
            return await CompensateAsync(claim, currentStep, compensations);
        }
    }

    private async Task<ClaimRecord> CompensateAsync(
        ClaimRecord claim,
        WorkflowStep failedStep,
        Stack<Compensation> compensations)
    {
        var failures = new List<CompensationFailure>();
        while (compensations.TryPop(out var compensation))
        {
            if (!await TryCompensateAsync(claim.ClaimId, compensation))
            {
                failures.Add(new CompensationFailure(compensation.Step, compensation.ResourceId));
            }
        }

        var compensatedClaim = claim with
        {
            Status = failures.Count == 0 ? ClaimStatus.Compensated : ClaimStatus.CompensationFailed,
            FailedStep = failedStep,
            CompensationFailures = failures,
        };
        await _store.SaveAsync(compensatedClaim, CancellationToken.None);
        return compensatedClaim;
    }

    /// <summary>
    /// Tries one compensation until it works or the attempts run out. It uses
    /// CancellationToken.None because the caller leaving must not leave money reserved.
    /// The timeout on each service client still limits how long one attempt can take.
    /// </summary>
    private async Task<bool> TryCompensateAsync(Guid claimId, Compensation compensation)
    {
        for (var attempt = 1; attempt <= _options.CompensationAttempts; attempt++)
        {
            try
            {
                await compensation.UndoAsync(CancellationToken.None);
                return true;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Claim {ClaimId}: compensation of {Step} for {ResourceId} failed on attempt {Attempt}.",
                    claimId,
                    compensation.Step,
                    compensation.ResourceId,
                    attempt);
            }

            if (attempt < _options.CompensationAttempts)
            {
                await Task.Delay(_options.CompensationRetryDelay);
            }
        }

        _logger.LogError(
            "Claim {ClaimId} needs manual repair: {Step} for {ResourceId} could not be compensated.",
            claimId,
            compensation.Step,
            compensation.ResourceId);
        return false;
    }

    /// <summary>
    /// Sends the email after the claim is accepted. A failure here only leaves
    /// NotificationSent false, so a later job can send the email again.
    /// </summary>
    private async Task<ClaimRecord> NotifyClaimantAsync(ClaimRecord claim, CancellationToken cancellationToken)
    {
        try
        {
            await _notifications.SendClaimReceivedAsync(claim.ClaimantEmail, claim.ClaimId, cancellationToken);
            var notifiedClaim = claim with { NotificationSent = true };
            await _store.SaveAsync(notifiedClaim, CancellationToken.None);
            return notifiedClaim;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Claim {ClaimId} was submitted, but the email failed.", claim.ClaimId);
            return claim;
        }
    }

    private sealed record Compensation(WorkflowStep Step, string ResourceId, Func<CancellationToken, Task> UndoAsync);
}
