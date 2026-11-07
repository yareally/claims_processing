using Claims.Api.Services;
using Claims.Api.Workflow;
using static Claims.Tests.ServiceCall;

namespace Claims.Tests;

/// <summary>Acceptance criteria 1 to 4.</summary>
public sealed class Level1CoreWorkflowTests
{
    private readonly WorkflowTestContext _context = new();

    [Fact]
    public async Task Success_calls_the_services_in_order()
    {
        await _context.SubmitAsync();

        Assert.Equal(
            new[] { CheckCoverage, CreateReserve, OpenCase, AssignAdjuster, SendNotification },
            _context.Services.Calls);
    }

    [Fact]
    public async Task Success_returns_a_submitted_claim_with_the_service_ids()
    {
        var claim = await _context.SubmitAsync();

        Assert.Equal(ClaimStatus.Submitted, claim.Status);
        Assert.Equal(FakeServices.ReserveId, claim.ReserveId);
        Assert.Equal(FakeServices.CaseId, claim.CaseId);
        Assert.Equal(FakeServices.AssignmentId, claim.AssignmentId);
        Assert.True(claim.NotificationSent);
        Assert.Null(claim.FailedStep);
    }

    [Fact]
    public async Task Success_saves_the_final_claim()
    {
        var claim = await _context.SubmitAsync();

        Assert.Equal(claim, await _context.SavedCopyOfAsync(claim));
    }

    [Fact]
    public async Task A_policy_without_coverage_rejects_the_claim_and_calls_nothing_else()
    {
        _context.Services.Coverage = new CoverageResult(false, "Policy lapsed");

        var claim = await _context.SubmitAsync();

        Assert.Equal(ClaimStatus.Rejected, claim.Status);
        Assert.Equal("Policy lapsed", claim.RejectionReason);
        Assert.Equal(new[] { CheckCoverage }, _context.Services.Calls);
        Assert.Equal(claim, await _context.SavedCopyOfAsync(claim));
    }

    public static TheoryData<ServiceCall, WorkflowStep, ServiceCall[], string[]> FailedSteps => new()
    {
        { CheckCoverage, WorkflowStep.CheckCoverage, [CheckCoverage], [] },
        { CreateReserve, WorkflowStep.CreateReserve, [CheckCoverage, CreateReserve], [] },
        {
            OpenCase,
            WorkflowStep.OpenCase,
            [CheckCoverage, CreateReserve, OpenCase, ReleaseReserve],
            [FakeServices.ReserveId]
        },
        {
            AssignAdjuster,
            WorkflowStep.AssignAdjuster,
            [CheckCoverage, CreateReserve, OpenCase, AssignAdjuster, CancelCase, ReleaseReserve],
            [FakeServices.CaseId, FakeServices.ReserveId]
        },
    };

    [Theory]
    [MemberData(nameof(FailedSteps))]
    public async Task A_failed_step_compensates_the_completed_steps_newest_first(
        ServiceCall failingCall,
        WorkflowStep expectedFailedStep,
        ServiceCall[] expectedCalls,
        string[] expectedResourceIds)
    {
        _context.Services.FailAlways(failingCall);

        var claim = await _context.SubmitAsync();

        Assert.Equal(ClaimStatus.Compensated, claim.Status);
        Assert.Equal(expectedFailedStep, claim.FailedStep);
        Assert.Equal(expectedCalls, _context.Services.Calls);
        Assert.Equal(expectedResourceIds, _context.Services.ResourceIdsCompensated);
        Assert.Equal(ClaimStatus.Compensated, (await _context.SavedCopyOfAsync(claim)).Status);
    }
}
