using Claims.Api.Workflow;

namespace Claims.Api;

/// <summary>The JSON body of POST /claims.</summary>
public sealed record SubmitClaimRequest(
    string PolicyNumber,
    string ClaimantEmail,
    DateOnly LossDate,
    decimal EstimatedAmount)
{
    /// <summary>Returns the errors for each invalid field. An empty result means the request is valid.</summary>
    public Dictionary<string, string[]> Validate(DateOnly today)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(PolicyNumber))
        {
            errors[nameof(PolicyNumber)] = ["A policy number is required."];
        }

        if (string.IsNullOrWhiteSpace(ClaimantEmail) || !ClaimantEmail.Contains('@'))
        {
            errors[nameof(ClaimantEmail)] = ["A valid email address is required."];
        }

        if (LossDate == default || LossDate > today)
        {
            errors[nameof(LossDate)] = ["The loss date is required and cannot be in the future."];
        }

        if (EstimatedAmount <= 0)
        {
            errors[nameof(EstimatedAmount)] = ["The estimated amount must be greater than zero."];
        }

        return errors;
    }
}

/// <summary>
/// The JSON body returned to the caller. It leaves out the email address and the
/// compensation details, which only the support team needs.
/// </summary>
public sealed record ClaimResponse(
    Guid ClaimId,
    ClaimStatus Status,
    string? CaseId,
    string? RejectionReason,
    WorkflowStep? FailedStep,
    bool NotificationSent)
{
    public static ClaimResponse From(ClaimRecord claim) => new(
        claim.ClaimId,
        claim.Status,
        claim.CaseId,
        claim.RejectionReason,
        claim.FailedStep,
        claim.NotificationSent);
}
