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

       ClaimRecord claimRecord = null;
       var coverResult = await _policy.CheckCoverageAsync(request.PolicyNumber, request.Date, cancellationToken);

     
        if (!coverResult.IsCovered)
        {
            // claimRecord = _store.GetOrAddAsync(idempotencyKey, request.ClaimRecord, cancellationToken);
            var rejectClaim = request.ClaimRecord with {ClaimStatus = ClaimStatus.Rejected, RejectionReason = coverResult.reason};
            return rejectClaim;
            
        };

        try
        {
        var claimReserveId = await _reserve.CreateReserveAsync(request.ClaimId, request.policyNumber, request.amount, cancellationToken);

        var caseId = await _cases.OpenCaseAsync(request.claimId, request.policyNumber, cancellationToken);

        var adjusterId = await _adjusters.AssignAdjusterAsync(caseId, cancellationToken);
        await _notifications.SendClaimReceivedAsync(request.email, request.claimId, cancellationToken);

        var successfulClaimRecord = request.claimRecord with 
        {ClaimStatus = ClaimStatus.Submitted, ReserveId = claimReserveId, CaseId = caseId, AssignmentId = adjusterId};
        return successfulClaimRecord;

        } catch (Exception exception)
        {
            
        }
    
        
      
       
    }

};
