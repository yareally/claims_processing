using System.Net;
using Claims.Api.Workflow;
using static Claims.Tests.ServiceCall;

namespace Claims.Tests;

/// <summary>Item 11 of the acceptance criteria.</summary>
public sealed class Level3DuplicateSubmissionTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    private readonly WorkflowTestContext _context = new();

    [Fact]
    public async Task The_same_key_returns_the_same_claim_and_runs_no_step_again()
    {
        var first = await _context.SubmitAsync("key-1");
        var callsAfterFirst = _context.Services.Calls.Count;

        var second = await _context.SubmitAsync("key-1");

        Assert.Equal(first, second);
        Assert.Equal(callsAfterFirst, _context.Services.Calls.Count);
    }

    [Fact]
    public async Task The_same_key_after_a_compensation_returns_the_compensated_claim()
    {
        _context.Services.FailAlways(OpenCase);
        var first = await _context.SubmitAsync("key-1");

        var second = await _context.SubmitAsync("key-1");

        Assert.Equal(ClaimStatus.Compensated, second.Status);
        Assert.Equal(first.ClaimId, second.ClaimId);
        Assert.Equal(1, _context.Services.CountOf(CreateReserve));
    }

    [Fact]
    public async Task Different_keys_create_different_claims()
    {
        var first = await _context.SubmitAsync("key-1");
        var second = await _context.SubmitAsync("key-2");

        Assert.NotEqual(first.ClaimId, second.ClaimId);
        Assert.Equal(2, _context.Services.CountOf(CreateReserve));
    }

    [Fact]
    public async Task A_duplicate_during_the_first_run_sees_in_progress_and_runs_no_step()
    {
        var firstIsRunning = new TaskCompletionSource();
        var letFirstFinish = new TaskCompletionSource();
        _context.Services.BeforeCall = async call =>
        {
            if (call == CheckCoverage)
            {
                firstIsRunning.TrySetResult();
                await letFirstFinish.Task;
            }
        };

        var firstRun = _context.SubmitAsync("key-1");
        await Task.WhenAny(firstIsRunning.Task, firstRun).WaitAsync(TestTimeout);
        var duplicate = await _context.SubmitAsync("key-1").WaitAsync(TestTimeout);
        letFirstFinish.SetResult();
        var first = await firstRun.WaitAsync(TestTimeout);

        Assert.Equal(ClaimStatus.InProgress, duplicate.Status);
        Assert.Equal(first.ClaimId, duplicate.ClaimId);
        Assert.Equal(ClaimStatus.Submitted, first.Status);
        Assert.Equal(1, _context.Services.CountOf(CheckCoverage));
    }
}

/// <summary>The 409 row of the status code table.</summary>
public sealed class Level3ApiTests : IDisposable
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    private readonly ApiTestContext _context = new();

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task A_duplicate_during_the_first_run_returns_409()
    {
        var firstIsRunning = new TaskCompletionSource();
        var letFirstFinish = new TaskCompletionSource();
        _context.Services.BeforeCall = async call =>
        {
            if (call == CheckCoverage)
            {
                firstIsRunning.TrySetResult();
                await letFirstFinish.Task;
            }
        };

        var firstRun = _context.PostClaimAsync(ApiTestContext.ValidRequest);
        await Task.WhenAny(firstIsRunning.Task, firstRun).WaitAsync(TestTimeout);
        var duplicate = await _context.PostClaimAsync(ApiTestContext.ValidRequest).WaitAsync(TestTimeout);
        letFirstFinish.SetResult();
        var first = await firstRun.WaitAsync(TestTimeout);

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
    }

    [Fact]
    public async Task A_repeat_after_the_first_run_returns_the_first_response()
    {
        var first = await ApiTestContext.ReadClaimAsync(await _context.PostClaimAsync(ApiTestContext.ValidRequest));

        var response = await _context.PostClaimAsync(ApiTestContext.ValidRequest);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(first, await ApiTestContext.ReadClaimAsync(response));
    }
}
