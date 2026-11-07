using System.Net;
using Claims.Api;
using Claims.Api.Services;
using Claims.Api.Workflow;
using static Claims.Tests.ServiceCall;

namespace Claims.Tests;

/// <summary>The status code table in the brief. The validation tests pass from the start.</summary>
public sealed class Level1ApiTests : IDisposable
{
    private readonly ApiTestContext _context = new();

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task A_submitted_claim_returns_201_with_its_location()
    {
        var response = await _context.PostClaimAsync(ApiTestContext.ValidRequest);
        var claim = await ApiTestContext.ReadClaimAsync(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(ClaimStatus.Submitted, claim.Status);
        Assert.Equal($"/claims/{claim.ClaimId}", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task A_rejected_claim_returns_422_with_the_reason()
    {
        _context.Services.Coverage = new CoverageResult(false, "Policy lapsed");

        var response = await _context.PostClaimAsync(ApiTestContext.ValidRequest);
        var claim = await ApiTestContext.ReadClaimAsync(response);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("Policy lapsed", claim.RejectionReason);
    }

    [Fact]
    public async Task A_compensated_claim_returns_503_and_names_the_failed_step()
    {
        _context.Services.FailAlways(AssignAdjuster);

        var response = await _context.PostClaimAsync(ApiTestContext.ValidRequest);
        var claim = await ApiTestContext.ReadClaimAsync(response);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(WorkflowStep.AssignAdjuster, claim.FailedStep);
    }

    [Fact]
    public async Task A_failed_compensation_returns_500()
    {
        _context.Services.FailAlways(AssignAdjuster);
        _context.Services.FailAlways(ReleaseReserve);

        var response = await _context.PostClaimAsync(ApiTestContext.ValidRequest);
        var claim = await ApiTestContext.ReadClaimAsync(response);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(ClaimStatus.CompensationFailed, claim.Status);
    }

    [Fact]
    public async Task Get_returns_a_claim_after_it_is_submitted()
    {
        var created = await ApiTestContext.ReadClaimAsync(await _context.PostClaimAsync(ApiTestContext.ValidRequest));

        var response = await _context.Client.GetAsync($"/claims/{created.ClaimId}");
        var claim = await ApiTestContext.ReadClaimAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(created, claim);
    }

    [Fact]
    public async Task Get_returns_404_for_an_unknown_claim()
    {
        var response = await _context.Client.GetAsync($"/claims/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_missing_idempotency_key_returns_400()
    {
        var response = await _context.PostClaimAsync(ApiTestContext.ValidRequest, idempotencyKey: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(_context.Services.Calls);
    }

    public static TheoryData<string, SubmitClaimRequest> InvalidRequests => new()
    {
        { "blank policy", ApiTestContext.ValidRequest with { PolicyNumber = " " } },
        { "email without @", ApiTestContext.ValidRequest with { ClaimantEmail = "pat.example.com" } },
        { "loss date not set", ApiTestContext.ValidRequest with { LossDate = default } },
        { "loss date in the future", ApiTestContext.ValidRequest with { LossDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2) } },
        { "zero amount", ApiTestContext.ValidRequest with { EstimatedAmount = 0m } },
        { "negative amount", ApiTestContext.ValidRequest with { EstimatedAmount = -1m } },
    };

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task An_invalid_request_returns_400_and_calls_no_service(string reason, SubmitClaimRequest request)
    {
        var response = await _context.PostClaimAsync(request);

        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, reason);
        Assert.Empty(_context.Services.Calls);
    }
}
