namespace Claims.Api.Workflow;

public sealed class WorkflowOptions
{
    /// <summary>How many times to try each compensation in total, counting the first try.</summary>
    public int CompensationAttempts { get; init; } = 3;

    /// <summary>How long to wait between tries of a compensation.</summary>
    public TimeSpan CompensationRetryDelay { get; init; } = TimeSpan.FromMilliseconds(200);
}
