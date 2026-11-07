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

    public Task<ClaimRecord> SubmitAsync(
        SubmitClaimRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        // TODO: meet the acceptance criteria in docs/01_brief.md.
        // Add private helper methods as you need them.
        throw new NotImplementedException();
    }
}
