using Claims.Api.Workflow;
using static Claims.Tests.ServiceCall;

namespace Claims.Tests;

/// <summary>Brief items 6 to 10. These tests check the method SubmitAsync.</summary>
public sealed class Level2FailureHandlingTests
{
    private readonly WorkflowTestContext _context = new();

    [Fact]
    public async Task SubmitAsync_retries_an_undo_that_fails_briefly()
    {
        _context.Services.FailAlways(AssignAdjuster);
        _context.Services.FailNext(ReleaseReserve, times: 2);

        var claim = await _context.SubmitAsync();

        Assert.Equal(ClaimStatus.Compensated, claim.Status);
        Assert.Equal(3, _context.Services.CountOf(ReleaseReserve));
        Assert.Empty(claim.CompensationFailures);
    }

    [Fact]
    public async Task SubmitAsync_records_an_undo_that_keeps_failing()
    {
        _context.Services.FailAlways(AssignAdjuster);
        _context.Services.FailAlways(ReleaseReserve);

        var claim = await _context.SubmitAsync();

        Assert.Equal(ClaimStatus.CompensationFailed, claim.Status);
        Assert.Equal(WorkflowStep.AssignAdjuster, claim.FailedStep);
        Assert.Equal([new CompensationFailure(WorkflowStep.CreateReserve, FakeServices.ReserveId)], claim.CompensationFailures);
        Assert.Equal(3, _context.Services.CountOf(ReleaseReserve));
        Assert.Equal(ClaimStatus.CompensationFailed, (await _context.SavedCopyOfAsync(claim)).Status);
    }

    [Fact]
    public async Task SubmitAsync_continues_with_the_other_undos_after_one_fails()
    {
        _context.Services.FailAlways(AssignAdjuster);
        _context.Services.FailAlways(CancelCase);

        var claim = await _context.SubmitAsync();

        Assert.Equal(ClaimStatus.CompensationFailed, claim.Status);
        Assert.Equal([FakeServices.ReserveId], _context.Services.ResourceIdsCompensated);
        Assert.Equal([new CompensationFailure(WorkflowStep.OpenCase, FakeServices.CaseId)], claim.CompensationFailures);
    }

    [Fact]
    public async Task SubmitAsync_records_every_undo_that_fails()
    {
        _context.Services.FailAlways(AssignAdjuster);
        _context.Services.FailAlways(CancelCase);
        _context.Services.FailAlways(ReleaseReserve);

        var claim = await _context.SubmitAsync();

        Assert.Equal(ClaimStatus.CompensationFailed, claim.Status);
        Assert.Equal(
            [
                new CompensationFailure(WorkflowStep.OpenCase, FakeServices.CaseId),
                new CompensationFailure(WorkflowStep.CreateReserve, FakeServices.ReserveId),
            ],
            claim.CompensationFailures);
    }

    [Fact]
    public async Task SubmitAsync_still_undoes_the_steps_when_the_caller_cancels()
    {
        using var caller = new CancellationTokenSource();
        _context.Services.BeforeCall = call =>
        {
            if (call == OpenCase)
            {
                caller.Cancel();
            }

            return Task.CompletedTask;
        };

        var claim = await _context.SubmitAsync(cancellationToken: caller.Token);

        Assert.Equal(ClaimStatus.Compensated, claim.Status);
        Assert.Equal(WorkflowStep.OpenCase, claim.FailedStep);
        Assert.Equal([FakeServices.ReserveId], _context.Services.ResourceIdsCompensated);
        Assert.False(_context.Services.LastTokenFor(ReleaseReserve).IsCancellationRequested, "The undo must not use the caller's token.");
        Assert.Equal(ClaimStatus.Compensated, (await _context.SavedCopyOfAsync(claim)).Status);
    }

    [Fact]
    public async Task SubmitAsync_saves_each_id_before_the_next_step_starts()
    {
        ClaimRecord? savedWhenCaseOpens = null;
        ClaimRecord? savedWhenAdjusterIsAssigned = null;
        _context.Services.BeforeCall = async call =>
        {
            if (call == OpenCase)
            {
                savedWhenCaseOpens = await _context.Store.FindAsync(_context.Services.LastClaimId, CancellationToken.None);
            }

            if (call == AssignAdjuster)
            {
                savedWhenAdjusterIsAssigned = await _context.Store.FindAsync(_context.Services.LastClaimId, CancellationToken.None);
            }
        };

        await _context.SubmitAsync();

        Assert.Equal(FakeServices.ReserveId, savedWhenCaseOpens?.ReserveId);
        Assert.Equal(ClaimStatus.InProgress, savedWhenCaseOpens?.Status);
        Assert.Equal(FakeServices.CaseId, savedWhenAdjusterIsAssigned?.CaseId);
    }

    [Fact]
    public async Task SubmitAsync_failed_email_keeps_the_claim_submitted()
    {
        _context.Services.FailAlways(SendNotification);

        var claim = await _context.SubmitAsync();

        Assert.Equal(ClaimStatus.Submitted, claim.Status);
        Assert.False(claim.NotificationSent, "NotificationSent should be false.");
        Assert.Empty(_context.Services.ResourceIdsCompensated);
        Assert.Equal(ClaimStatus.Submitted, (await _context.SavedCopyOfAsync(claim)).Status);
    }
}
