using Claims.Api.Workflow;
using static Claims.Tests.ServiceCall;

namespace Claims.Tests;

/// <summary>Brief item 11. These tests check the method SubmitAsync.</summary>
public sealed class Level3DuplicateSubmissionTests
{
    private readonly WorkflowTestContext _context = new();

    [Fact]
    public async Task SubmitAsync_same_key_returns_the_same_claim_and_runs_no_step_again()
    {
        var first = await _context.SubmitAsync("key-1");

        var second = await _context.SubmitAsync("key-1");

        Assert.Equal(first, second);
        Assert.Equal([CheckCoverage, CreateReserve, OpenCase, AssignAdjuster, SendNotification], _context.Services.Calls);
    }

    [Fact]
    public async Task SubmitAsync_same_key_after_an_undo_returns_the_compensated_claim()
    {
        _context.Services.FailAlways(OpenCase);
        var first = await _context.SubmitAsync("key-1");

        var second = await _context.SubmitAsync("key-1");

        Assert.Equal(ClaimStatus.Compensated, second.Status);
        Assert.Equal(first.ClaimId, second.ClaimId);
        Assert.Equal(1, _context.Services.CountOf(CreateReserve));
    }

    [Fact]
    public void SubmitAsync_same_key_after_a_rejection_does_not_check_coverage_again()
    {
        // TODO: make the policy service return no coverage, then call SubmitAsync twice with one key.
        // Check that the second claim is Rejected and that CheckCoverage ran once.
        // Change this method to "async Task", because it needs await.
        Assert.Fail("This test is empty. Follow its TODO.");
    }

    [Fact]
    public async Task SubmitAsync_different_keys_create_different_claims()
    {
        var first = await _context.SubmitAsync("key-1");
        var second = await _context.SubmitAsync("key-2");

        Assert.NotEqual(first.ClaimId, second.ClaimId);
        Assert.Equal(2, _context.Services.CountOf(CreateReserve));
    }

    [Fact]
    public async Task SubmitAsync_duplicate_during_the_first_run_sees_in_progress_and_runs_no_step()
    {
        // The duplicate request arrives while the first request is at its coverage check.
        ClaimRecord? duplicate = null;
        var duplicateSent = false;
        _context.Services.BeforeCall = async call =>
        {
            if (call == CheckCoverage && !duplicateSent)
            {
                duplicateSent = true;
                duplicate = await _context.SubmitAsync("key-1");
            }
        };

        var first = await _context.SubmitAsync("key-1");

        Assert.Equal(1, _context.Services.CountOf(CheckCoverage));
        Assert.Equal(ClaimStatus.InProgress, duplicate?.Status);
        Assert.Equal(first.ClaimId, duplicate?.ClaimId);
        Assert.Equal(ClaimStatus.Submitted, first.Status);
    }
}
