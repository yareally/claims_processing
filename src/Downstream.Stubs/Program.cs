// One small app that stands in for the policy, reserve, case, adjuster and notification
// services, so the practice project needs no other setup. In production each would be its
// own service. The policy number decides which failure to act out (see TestPolicies).

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<StubState>();
var app = builder.Build();

app.MapPost("/coverage-checks", (CoverageRequest request, StubState state) =>
{
    state.RecordCall("POST /coverage-checks");
    return request.PolicyNumber == TestPolicies.Inactive
        ? Results.Ok(new { isCovered = false, reason = "The policy was not active on the loss date." })
        : Results.Ok(new { isCovered = true, reason = (string?)null });
});

app.MapPost("/reserves", (ReserveRequest request, StubState state) =>
{
    state.RecordCall("POST /reserves");
    return Results.Created((string?)null, new { id = state.OpenReserve(request.ClaimId, request.PolicyNumber) });
});

app.MapDelete("/reserves/{reserveId}", (string reserveId, StubState state) =>
{
    state.RecordCall("DELETE /reserves");
    return state.PolicyForReserve(reserveId) == TestPolicies.FailRelease
        ? Results.StatusCode(StatusCodes.Status503ServiceUnavailable)
        : state.ReleaseReserve(reserveId) ? Results.NoContent() : Results.NotFound();
});

app.MapPost("/cases", (CaseRequest request, StubState state) =>
{
    state.RecordCall("POST /cases");
    return request.PolicyNumber == TestPolicies.FailCase
        ? Results.StatusCode(StatusCodes.Status503ServiceUnavailable)
        : Results.Created((string?)null, new { id = state.OpenCase(request.ClaimId, request.PolicyNumber) });
});

app.MapDelete("/cases/{caseId}", (string caseId, StubState state) =>
{
    state.RecordCall("DELETE /cases");
    return state.CancelCase(caseId) ? Results.NoContent() : Results.NotFound();
});

app.MapPost("/assignments", (AssignmentRequest request, StubState state) =>
{
    state.RecordCall("POST /assignments");
    var policyNumber = state.PolicyForCase(request.CaseId);
    return policyNumber is TestPolicies.FailAdjuster or TestPolicies.FailRelease
        ? Results.StatusCode(StatusCodes.Status503ServiceUnavailable)
        : Results.Created((string?)null, new { id = $"ADJ-{request.CaseId}" });
});

app.MapPost("/notifications", (NotificationRequest request, StubState state) =>
{
    state.RecordCall("POST /notifications");
    return state.PolicyForClaim(request.ClaimId) == TestPolicies.FailNotify
        ? Results.StatusCode(StatusCodes.Status500InternalServerError)
        : Results.Accepted();
});

// Shows what is still open, so you can see whether compensation worked.
app.MapGet("/debug/state", (StubState state) => Results.Ok(state.Snapshot()));

app.Run();

/// <summary>Lets the test project start this app through WebApplicationFactory.</summary>
public sealed class StubsEntryPoint;

public sealed record StubSnapshot(IReadOnlyList<string> OpenReserves, IReadOnlyList<string> OpenCases, IReadOnlyList<string> Calls);

internal static class TestPolicies
{
    public const string Inactive = "POL-INACTIVE";
    public const string FailCase = "POL-FAIL-CASE";
    public const string FailAdjuster = "POL-FAIL-ADJUSTER";
    public const string FailNotify = "POL-FAIL-NOTIFY";

    /// <summary>The adjuster step fails, then the reserve cannot be released.</summary>
    public const string FailRelease = "POL-FAIL-RELEASE";
}

internal sealed record CoverageRequest(string PolicyNumber, DateOnly LossDate);
internal sealed record ReserveRequest(Guid ClaimId, string PolicyNumber, decimal Amount);
internal sealed record CaseRequest(Guid ClaimId, string PolicyNumber);
internal sealed record AssignmentRequest(string CaseId);
internal sealed record NotificationRequest(string Email, Guid ClaimId);
internal sealed record OpenItem(Guid ClaimId, string PolicyNumber);

internal sealed class StubState
{
    private readonly object _lock = new();
    private readonly List<string> _calls = [];
    private readonly Dictionary<string, OpenItem> _reserves = [];
    private readonly Dictionary<string, OpenItem> _cases = [];
    private readonly Dictionary<Guid, string> _policiesByClaim = [];
    private int _nextId;

    public void RecordCall(string call)
    {
        lock (_lock)
        {
            _calls.Add(call);
        }
    }

    public string OpenReserve(Guid claimId, string policyNumber) => Open(_reserves, "RES", claimId, policyNumber);

    public string OpenCase(Guid claimId, string policyNumber) => Open(_cases, "CASE", claimId, policyNumber);

    public bool ReleaseReserve(string reserveId) => Close(_reserves, reserveId);

    public bool CancelCase(string caseId) => Close(_cases, caseId);

    public string? PolicyForReserve(string reserveId) => FindPolicy(_reserves, reserveId);

    public string? PolicyForCase(string caseId) => FindPolicy(_cases, caseId);

    public string? PolicyForClaim(Guid claimId)
    {
        lock (_lock)
        {
            return _policiesByClaim.GetValueOrDefault(claimId);
        }
    }

    public StubSnapshot Snapshot()
    {
        lock (_lock)
        {
            return new StubSnapshot([.. _reserves.Keys], [.. _cases.Keys], [.. _calls]);
        }
    }

    private string Open(Dictionary<string, OpenItem> items, string prefix, Guid claimId, string policyNumber)
    {
        lock (_lock)
        {
            var id = $"{prefix}-{++_nextId}";
            items[id] = new OpenItem(claimId, policyNumber);
            _policiesByClaim[claimId] = policyNumber;
            return id;
        }
    }

    private bool Close(Dictionary<string, OpenItem> items, string id)
    {
        lock (_lock)
        {
            return items.Remove(id);
        }
    }

    private string? FindPolicy(Dictionary<string, OpenItem> items, string id)
    {
        lock (_lock)
        {
            return items.GetValueOrDefault(id)?.PolicyNumber;
        }
    }
}
