using System.Security.Claims;
using Claims.Api.Services;

namespace Claims.Api.Workflow;

/// <summary>Coordinates the services that handle a new claim. See docs/01_brief.md.</summary>
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
        // TODO: meet the acceptance criteria in docs/01_brief.md.
        // Add private helper methods as you need them.
        // await Task.CompletedTask; // Delete this line once your code awaits a call.
        // // return ClaimRecord.Start(request);

        string? failedStep = null;
        string? reserveId = null;
        string? caseId = null;
        string? assignmentId = null;
        var claim = await _store.GetOrAddAsync(idempotencyKey, ClaimRecord.Start(request), cancellationToken);
        _logger.LogInformation("Checking policy coverage for claim");
     

        try {
            
            // var checkCoverageWS = WorkflowStep.CheckCoverage;

            CoverageResult coverageResult = await _policy.CheckCoverageAsync(request.PolicyNumber, request.LossDate, cancellationToken);
            if (!coverageResult.IsCovered)
            {
                return claim  with {Status = ClaimStatus.Rejected, RejectionReason = coverageResult.Reason};
            }
            // var createReserveWS = WorkflowStep.CreateReserve;
            failedStep = "CreateReserve";
            reserveId = await _reserve.CreateReserveAsync(claim.ClaimId, claim.PolicyNumber, claim.EstimatedAmount,cancellationToken);
           
            // var openCaseWS = WorkflowStep.OpenCase;
            failedStep = "OpenCase";
            caseId = await _cases.OpenCaseAsync(claim.ClaimId, claim.PolicyNumber, cancellationToken);
             if (failedStep == "OpenCase")
            {
                await _reserve.ReleaseReserveAsync(reserveId, cancellationToken);
                return claim with {Status = ClaimStatus.Compensated, FailedStep = WorkflowStep.OpenCase};
            }

            // var assignAdjusterWS = WorkflowStep.AssignAdjuster;
            failedStep = "AssignAdjuster";
            assignmentId = await _adjusters.AssignAdjusterAsync(caseId, cancellationToken);

            await _notifications.SendClaimReceivedAsync(claim.ClaimantEmail, claim.ClaimId, cancellationToken);
            // await Task.CompletedTask;

            await _store.SaveAsync(claim with {ReserveId = reserveId, CaseId = caseId, AssignmentId = assignmentId, Status = ClaimStatus.Submitted, NotificationSent = true}, cancellationToken);

        } catch (Exception exception)
        {
            if (failedStep == "CreateReserve")
            {
                await _reserve.ReleaseReserveAsync(reserveId, cancellationToken);
                return claim with {Status = ClaimStatus.Compensated, FailedStep = WorkflowStep.CreateReserve};
            }
            
        }
        // await _store.SaveAsync(claim, cancellationToken);
        return claim;
    }
}
