using Claims.Api;
using Claims.Api.Workflow;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Claims.Tests;

/// <summary>Builds a workflow on top of FakeServices, with no retry delay unless a test sets one.</summary>
public sealed class WorkflowTestContext
{
    public const string ClaimantEmail = "pat@example.com";

    public WorkflowTestContext(
        WorkflowOptions? options = null,
        IClaimStore? store = null,
        ILogger<ClaimSubmissionWorkflow>? logger = null)
    {
        Store = store ?? new InMemoryClaimStore();
        Workflow = new ClaimSubmissionWorkflow(
            Services,
            Services,
            Services,
            Services,
            Services,
            Store,
            options ?? new WorkflowOptions { CompensationRetryDelay = TimeSpan.Zero },
            logger ?? NullLogger<ClaimSubmissionWorkflow>.Instance);
    }

    public FakeServices Services { get; } = new();

    public IClaimStore Store { get; }

    public ClaimSubmissionWorkflow Workflow { get; }

    public static SubmitClaimRequest ValidRequest { get; } =
        new("POL-100", ClaimantEmail, new DateOnly(2026, 1, 15), 2500m);

    public Task<ClaimRecord> SubmitAsync(string idempotencyKey = "key-1", CancellationToken cancellationToken = default) =>
        Workflow.SubmitAsync(ValidRequest, idempotencyKey, cancellationToken);

    public async Task<ClaimRecord> SavedCopyOfAsync(ClaimRecord claim)
    {
        var saved = await Store.FindAsync(claim.ClaimId, CancellationToken.None);
        return Assert.IsType<ClaimRecord>(saved);
    }
}
