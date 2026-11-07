namespace Claims.Api.Workflow;

public enum ClaimStatus
{
    /// <summary>The workflow is still running.</summary>
    InProgress,

    /// <summary>All required steps finished. The claim is accepted.</summary>
    Submitted,

    /// <summary>The policy does not cover this claim. Nothing needed undoing.</summary>
    Rejected,

    /// <summary>A required step failed and every completed step was undone.</summary>
    Compensated,

    /// <summary>A required step failed and at least one completed step could not be undone.</summary>
    CompensationFailed,
}

/// <summary>The required steps of the workflow, in the order they run.</summary>
public enum WorkflowStep
{
    CheckCoverage,
    CreateReserve,
    OpenCase,
    AssignAdjuster,
}

/// <summary>A completed step that could not be undone, so a person must fix it by hand.</summary>
public sealed record CompensationFailure(WorkflowStep Step, string ResourceId);

/// <summary>
/// The saved state of one claim. It cannot be changed in place: use a "with" expression
/// to make an updated copy, then save the copy.
/// </summary>
public sealed record ClaimRecord
{
    public required Guid ClaimId { get; init; }
    public required string PolicyNumber { get; init; }
    public required string ClaimantEmail { get; init; }
    public required DateOnly LossDate { get; init; }
    public required decimal EstimatedAmount { get; init; }

    public ClaimStatus Status { get; init; } = ClaimStatus.InProgress;
    public string? ReserveId { get; init; }
    public string? CaseId { get; init; }
    public string? AssignmentId { get; init; }
    public bool NotificationSent { get; init; }
    public string? RejectionReason { get; init; }
    public WorkflowStep? FailedStep { get; init; }
    public IReadOnlyList<CompensationFailure> CompensationFailures { get; init; } = [];

    /// <summary>Creates the record for a new claim, with a new ID and the status InProgress.</summary>
    public static ClaimRecord Start(SubmitClaimRequest request) => new()
    {
        ClaimId = Guid.NewGuid(),
        PolicyNumber = request.PolicyNumber,
        ClaimantEmail = request.ClaimantEmail,
        LossDate = request.LossDate,
        EstimatedAmount = request.EstimatedAmount,
    };
}
