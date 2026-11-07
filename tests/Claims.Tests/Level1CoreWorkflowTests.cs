using Claims.Api.Services;
using Claims.Api.Workflow;
using static Claims.Tests.ServiceCall;

namespace Claims.Tests;

/// <summary>Brief items 1 to 4. These tests check the method SubmitAsync.</summary>
public sealed class Level1CoreWorkflowTests
{
    private readonly WorkflowTestContext _context = new();

    [Fact]
    public async Task SubmitAsync_good_claim_calls_the_services_in_order()
    {
        await _context.SubmitAsync();

        Assert.Equal([CheckCoverage, CreateReserve, OpenCase, AssignAdjuster, SendNotification], _context.Services.Calls);
    }

    [Fact]
    public async Task SubmitAsync_good_claim_is_submitted_with_the_service_ids()
    {
        var claim = await _context.SubmitAsync();

        Assert.Equal(ClaimStatus.Submitted, claim.Status);
        Assert.Equal(FakeServices.ReserveId, claim.ReserveId);
        Assert.Equal(FakeServices.CaseId, claim.CaseId);
        Assert.Equal(FakeServices.AssignmentId, claim.AssignmentId);
        Assert.True(claim.NotificationSent, "NotificationSent should be true.");
        Assert.Null(claim.FailedStep);
    }

    [Fact]
    public async Task SubmitAsync_good_claim_is_saved()
    {
        var claim = await _context.SubmitAsync();

        var saved = await _context.SavedCopyOfAsync(claim);

        Assert.Equal(ClaimStatus.Submitted, saved.Status);
        Assert.Equal(FakeServices.AssignmentId, saved.AssignmentId);
    }

    [Fact]
    public async Task SubmitAsync_policy_without_coverage_is_rejected_and_calls_nothing_else()
    {
        _context.Services.Coverage = new CoverageResult(false, "Policy lapsed");

        var claim = await _context.SubmitAsync();

        Assert.Equal(ClaimStatus.Rejected, claim.Status);
        Assert.Equal("Policy lapsed", claim.RejectionReason);
        Assert.Equal([CheckCoverage], _context.Services.Calls);
        Assert.Equal(ClaimStatus.Rejected, (await _context.SavedCopyOfAsync(claim)).Status);
    }

    [Fact]
    public async Task SubmitAsync_when_coverage_check_fails_undoes_nothing()
    {
        _context.Services.FailAlways(CheckCoverage);

        var claim = await _context.SubmitAsync();

        Assert.Equal(ClaimStatus.Compensated, claim.Status);
        Assert.Equal(WorkflowStep.CheckCoverage, claim.FailedStep);
        Assert.Equal([CheckCoverage], _context.Services.Calls);
    }

    [Fact]
    public async Task SubmitAsync_when_reserve_step_fails_undoes_nothing()
    {
        _context.Services.FailAlways(CreateReserve);

        var claim = await _context.SubmitAsync();

        Assert.Equal(ClaimStatus.Compensated, claim.Status);
        Assert.Equal(WorkflowStep.CreateReserve, claim.FailedStep);
        Assert.Equal([CheckCoverage, CreateReserve], _context.Services.Calls);
    }

    [Fact]
    public async Task SubmitAsync_when_case_step_fails_releases_the_reserve()
    {
        _context.Services.FailAlways(OpenCase);

        var claim = await _context.SubmitAsync();

        Assert.Equal(ClaimStatus.Compensated, claim.Status);
        Assert.Equal(WorkflowStep.OpenCase, claim.FailedStep);
        Assert.Equal([CheckCoverage, CreateReserve, OpenCase, ReleaseReserve], _context.Services.Calls);
        Assert.Equal(ClaimStatus.Compensated, (await _context.SavedCopyOfAsync(claim)).Status);
    }

    [Fact]
    public async Task SubmitAsync_when_adjuster_step_fails_undoes_the_completed_steps()
    {
        _context.Services.FailAlways(AssignAdjuster);

        var claim = await _context.SubmitAsync();

        Assert.Equal(ClaimStatus.Compensated, claim.Status);
        Assert.Equal(WorkflowStep.AssignAdjuster, claim.FailedStep);
        Assert.Equal([CheckCoverage, CreateReserve, OpenCase, AssignAdjuster, ReleaseReserve, CancelCase], _context.Services.Calls);
    }
}
