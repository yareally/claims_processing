using Claims.Api.Services;

namespace Claims.Tests;

public enum ServiceCall
{
    CheckCoverage,
    CreateReserve,
    ReleaseReserve,
    OpenCase,
    CancelCase,
    AssignAdjuster,
    SendNotification,
}

/// <summary>
/// Stands in for all five services. It records each call in order and throws when a test
/// asks it to fail.
/// </summary>
public sealed class FakeServices :
    IPolicyService, IReserveService, ICaseService, IAdjusterService, INotificationService
{
    public const string ReserveId = "RES-1";
    public const string CaseId = "CASE-1";
    public const string AssignmentId = "ADJ-1";

    private readonly object _lock = new();
    private readonly List<ServiceCall> _calls = [];
    private readonly List<string> _resourceIdsCompensated = [];
    private readonly Dictionary<ServiceCall, CancellationToken> _lastTokens = [];
    private readonly Dictionary<ServiceCall, int> _failuresLeft = [];
    private readonly Dictionary<ServiceCall, Exception> _exceptions = [];

    public CoverageResult Coverage { get; set; } = new(true, null);

    /// <summary>Runs at the start of each call, so a test can act at an exact point in the workflow.</summary>
    public Func<ServiceCall, Task>? BeforeCall { get; set; }

    /// <summary>Runs after a call succeeds, so a test can act between two steps.</summary>
    public Func<ServiceCall, Task>? AfterCall { get; set; }

    public Guid LastClaimId { get; private set; }

    public IReadOnlyList<ServiceCall> Calls
    {
        get
        {
            lock (_lock)
            {
                return [.. _calls];
            }
        }
    }

    public IReadOnlyList<string> ResourceIdsCompensated
    {
        get
        {
            lock (_lock)
            {
                return [.. _resourceIdsCompensated];
            }
        }
    }

    public void FailAlways(ServiceCall call, Exception? exception = null) => FailNext(call, int.MaxValue, exception);

    public void FailNext(ServiceCall call, int times, Exception? exception = null)
    {
        lock (_lock)
        {
            _failuresLeft[call] = times;
            _exceptions[call] = exception ?? new DownstreamServiceException(call.ToString(), "the test asked for a failure");
        }
    }

    public CancellationToken LastTokenFor(ServiceCall call)
    {
        lock (_lock)
        {
            return _lastTokens[call];
        }
    }

    public int CountOf(ServiceCall call) => Calls.Count(recorded => recorded == call);

    public async Task<CoverageResult> CheckCoverageAsync(string policyNumber, DateOnly lossDate, CancellationToken cancellationToken)
    {
        await RecordAsync(ServiceCall.CheckCoverage, cancellationToken);
        return Coverage;
    }

    public async Task<string> CreateReserveAsync(Guid claimId, string policyNumber, decimal amount, CancellationToken cancellationToken)
    {
        LastClaimId = claimId;
        await RecordAsync(ServiceCall.CreateReserve, cancellationToken);
        return ReserveId;
    }

    public async Task ReleaseReserveAsync(string reserveId, CancellationToken cancellationToken)
    {
        await RecordAsync(ServiceCall.ReleaseReserve, cancellationToken);
        RememberCompensated(reserveId);
    }

    public async Task<string> OpenCaseAsync(Guid claimId, string policyNumber, CancellationToken cancellationToken)
    {
        LastClaimId = claimId;
        await RecordAsync(ServiceCall.OpenCase, cancellationToken);
        return CaseId;
    }

    public async Task CancelCaseAsync(string caseId, CancellationToken cancellationToken)
    {
        await RecordAsync(ServiceCall.CancelCase, cancellationToken);
        RememberCompensated(caseId);
    }

    public async Task<string> AssignAdjusterAsync(string caseId, CancellationToken cancellationToken)
    {
        await RecordAsync(ServiceCall.AssignAdjuster, cancellationToken);
        return AssignmentId;
    }

    public async Task SendClaimReceivedAsync(string email, Guid claimId, CancellationToken cancellationToken)
    {
        await RecordAsync(ServiceCall.SendNotification, cancellationToken);
    }

    private async Task RecordAsync(ServiceCall call, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            _calls.Add(call);
            _lastTokens[call] = cancellationToken;
        }

        if (BeforeCall is not null)
        {
            await BeforeCall(call);
        }

        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfAskedToFail(call);

        if (AfterCall is not null)
        {
            await AfterCall(call);
        }
    }

    private void ThrowIfAskedToFail(ServiceCall call)
    {
        lock (_lock)
        {
            if (_failuresLeft.GetValueOrDefault(call) > 0)
            {
                _failuresLeft[call]--;
                throw _exceptions[call];
            }
        }
    }

    private void RememberCompensated(string resourceId)
    {
        lock (_lock)
        {
            _resourceIdsCompensated.Add(resourceId);
        }
    }
}
