using System.Diagnostics;
using Claims.Api.Workflow;
using Microsoft.Extensions.Logging;
using static Claims.Tests.ServiceCall;

namespace Claims.Tests.Hidden;

/// <summary>
/// Checks for cases the starter tests leave out. Copy this folder onto your own branch
/// after the timed run. They use only the types the starter gives you.
/// </summary>
public sealed class HiddenChecks
{
    [Fact]
    public async Task Compensation_stops_after_the_configured_number_of_attempts()
    {
        var context = new WorkflowTestContext(new WorkflowOptions
        {
            CompensationAttempts = 2,
            CompensationRetryDelay = TimeSpan.Zero,
        });
        context.Services.FailAlways(OpenCase);
        context.Services.FailAlways(ReleaseReserve);

        await context.SubmitAsync();

        Assert.Equal(2, context.Services.CountOf(ReleaseReserve));
    }

    [Fact]
    public async Task Compensation_waits_between_attempts()
    {
        var delay = TimeSpan.FromMilliseconds(50);
        var context = new WorkflowTestContext(new WorkflowOptions
        {
            CompensationAttempts = 3,
            CompensationRetryDelay = delay,
        });
        context.Services.FailAlways(OpenCase);
        context.Services.FailAlways(ReleaseReserve);
        var stopwatch = Stopwatch.StartNew();

        await context.SubmitAsync();

        // Three attempts have two waits between them. The margin allows for timer rounding.
        Assert.True(stopwatch.Elapsed >= delay * 2 - TimeSpan.FromMilliseconds(10), $"Elapsed: {stopwatch.Elapsed}");
    }

    [Fact]
    public async Task An_unexpected_exception_from_a_step_still_compensates()
    {
        var context = new WorkflowTestContext();
        context.Services.FailAlways(OpenCase, new InvalidOperationException("A bug, not a service failure"));

        var claim = await context.SubmitAsync();

        Assert.Equal(ClaimStatus.Compensated, claim.Status);
        Assert.Equal(new[] { FakeServices.ReserveId }, context.Services.ResourceIdsCompensated);
    }

    [Fact]
    public async Task An_unexpected_exception_from_a_compensation_does_not_escape()
    {
        var context = new WorkflowTestContext();
        context.Services.FailAlways(OpenCase);
        context.Services.FailAlways(ReleaseReserve, new InvalidOperationException("A bug in the undo"));

        var claim = await context.SubmitAsync();

        Assert.Equal(ClaimStatus.CompensationFailed, claim.Status);
    }

    [Fact]
    public async Task A_failed_save_after_a_step_still_compensates_that_step()
    {
        var store = new FailingStore(claim => claim is { ReserveId: not null, CaseId: null, Status: ClaimStatus.InProgress });
        var context = new WorkflowTestContext(store: store);

        var claim = await context.SubmitAsync();

        Assert.Equal(ClaimStatus.Compensated, claim.Status);
        Assert.Equal(new[] { FakeServices.ReserveId }, context.Services.ResourceIdsCompensated);
    }

    [Fact]
    public async Task A_caller_leaving_during_the_email_keeps_the_claim_submitted()
    {
        using var caller = new CancellationTokenSource();
        var context = new WorkflowTestContext();
        context.Services.BeforeCall = call =>
        {
            if (call == SendNotification)
            {
                caller.Cancel();
            }

            return Task.CompletedTask;
        };

        var claim = await context.SubmitAsync(cancellationToken: caller.Token);

        Assert.Equal(ClaimStatus.Submitted, claim.Status);
        Assert.False(claim.NotificationSent);
        Assert.Empty(context.Services.ResourceIdsCompensated);
    }

    [Fact]
    public async Task A_caller_leaving_during_the_last_required_step_is_still_compensated_and_saved()
    {
        using var caller = new CancellationTokenSource();
        var context = new WorkflowTestContext();
        context.Services.BeforeCall = call =>
        {
            if (call == AssignAdjuster)
            {
                caller.Cancel();
            }

            return Task.CompletedTask;
        };

        var claim = await context.SubmitAsync(cancellationToken: caller.Token);

        Assert.Equal(ClaimStatus.Compensated, (await context.SavedCopyOfAsync(claim)).Status);
        Assert.Equal(new[] { FakeServices.CaseId, FakeServices.ReserveId }, context.Services.ResourceIdsCompensated);
    }

    [Fact]
    public async Task A_caller_leaving_right_after_the_last_required_step_keeps_the_claim_submitted()
    {
        using var caller = new CancellationTokenSource();
        var context = new WorkflowTestContext();
        context.Services.AfterCall = call =>
        {
            if (call == AssignAdjuster)
            {
                caller.Cancel();
            }

            return Task.CompletedTask;
        };

        var claim = await context.SubmitAsync(cancellationToken: caller.Token);

        Assert.Equal(ClaimStatus.Submitted, claim.Status);
        Assert.Empty(context.Services.ResourceIdsCompensated);
        Assert.Equal(ClaimStatus.Submitted, (await context.SavedCopyOfAsync(claim)).Status);
    }

    [Fact]
    public async Task The_email_address_never_appears_in_the_logs()
    {
        var logger = new RecordingLogger();
        var context = new WorkflowTestContext(logger: logger);
        context.Services.FailAlways(SendNotification);
        await context.SubmitAsync("key-1");
        context.Services.FailAlways(OpenCase);
        context.Services.FailAlways(ReleaseReserve);
        await context.SubmitAsync("key-2");

        Assert.NotEmpty(logger.Messages);
        Assert.DoesNotContain(logger.Messages, message => message.Contains(WorkflowTestContext.ClaimantEmail));
    }

    [Fact]
    public async Task A_failure_is_logged_with_the_claim_id_so_support_can_find_it()
    {
        var logger = new RecordingLogger();
        var context = new WorkflowTestContext(logger: logger);
        context.Services.FailAlways(OpenCase);

        var claim = await context.SubmitAsync();

        Assert.Contains(logger.Messages, message => message.Contains(claim.ClaimId.ToString()));
    }

    private sealed class FailingStore(Func<ClaimRecord, bool> failWhen) : IClaimStore
    {
        private readonly InMemoryClaimStore _inner = new();

        public Task<ClaimRecord> GetOrAddAsync(string idempotencyKey, ClaimRecord claim, CancellationToken cancellationToken) =>
            _inner.GetOrAddAsync(idempotencyKey, claim, cancellationToken);

        public Task SaveAsync(ClaimRecord claim, CancellationToken cancellationToken) =>
            failWhen(claim)
                ? throw new InvalidOperationException("The test asked for a failed save.")
                : _inner.SaveAsync(claim, cancellationToken);

        public Task<ClaimRecord?> FindAsync(Guid claimId, CancellationToken cancellationToken) =>
            _inner.FindAsync(claimId, cancellationToken);
    }

    private sealed class RecordingLogger : ILogger<ClaimSubmissionWorkflow>
    {
        private readonly object _lock = new();
        private readonly List<string> _messages = [];

        public IReadOnlyList<string> Messages
        {
            get
            {
                lock (_lock)
                {
                    return [.. _messages];
                }
            }
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            lock (_lock)
            {
                _messages.Add($"{formatter(state, exception)} {exception}");
            }
        }
    }
}
