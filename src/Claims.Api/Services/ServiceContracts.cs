namespace Claims.Api.Services;

public sealed record CoverageResult(bool IsCovered, string? Reason);

public interface IPolicyService
{
    Task<CoverageResult> CheckCoverageAsync(string policyNumber, DateOnly lossDate, CancellationToken cancellationToken);
}

public interface IReserveService
{
    /// <summary>Sets money aside for the claim and returns the reserve ID.</summary>
    Task<string> CreateReserveAsync(Guid claimId, string policyNumber, decimal amount, CancellationToken cancellationToken);

    /// <summary>Gives the money back. Calling this twice for one reserve is safe.</summary>
    Task ReleaseReserveAsync(string reserveId, CancellationToken cancellationToken);
}

public interface ICaseService
{
    /// <summary>Opens a case in the claims system and returns the case ID.</summary>
    Task<string> OpenCaseAsync(Guid claimId, string policyNumber, CancellationToken cancellationToken);

    /// <summary>Cancels the case and frees its adjuster. Calling this twice for one case is safe.</summary>
    Task CancelCaseAsync(string caseId, CancellationToken cancellationToken);
}

public interface IAdjusterService
{
    /// <summary>Assigns an adjuster to the case and returns the assignment ID.</summary>
    Task<string> AssignAdjusterAsync(string caseId, CancellationToken cancellationToken);
}

public interface INotificationService
{
    /// <summary>Emails the claimant. An email cannot be recalled, so this has no undo.</summary>
    Task SendClaimReceivedAsync(string email, Guid claimId, CancellationToken cancellationToken);
}

/// <summary>A service could not be reached, timed out, or returned an error status.</summary>
public sealed class DownstreamServiceException(string path, string problem, Exception? innerException = null)
    : Exception($"Call to {path} failed: {problem}", innerException);
