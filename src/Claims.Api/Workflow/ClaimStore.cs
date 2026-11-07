namespace Claims.Api.Workflow;

public interface IClaimStore
{
    /// <summary>
    /// Stores <paramref name="claim"/> under the key, unless the key already has a claim.
    /// Returns the claim held for the key. The check and the add happen as one atomic
    /// action, so two requests at the same time cannot both add a claim.
    /// </summary>
    Task<ClaimRecord> GetOrAddAsync(string idempotencyKey, ClaimRecord claim, CancellationToken cancellationToken);

    /// <summary>Replaces the stored copy of the claim.</summary>
    Task SaveAsync(ClaimRecord claim, CancellationToken cancellationToken);

    Task<ClaimRecord?> FindAsync(Guid claimId, CancellationToken cancellationToken);
}

/// <summary>
/// Keeps claims in memory. It stands in for a database, so like a database call it
/// throws when given a token that has already been cancelled.
/// </summary>
public sealed class InMemoryClaimStore : IClaimStore
{
    private readonly object _lock = new();
    private readonly Dictionary<string, Guid> _claimIdsByKey = [];
    private readonly Dictionary<Guid, ClaimRecord> _claimsById = [];

    public Task<ClaimRecord> GetOrAddAsync(
        string idempotencyKey,
        ClaimRecord claim,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_lock)
        {
            if (_claimIdsByKey.TryGetValue(idempotencyKey, out var existingId))
            {
                return Task.FromResult(_claimsById[existingId]);
            }

            _claimIdsByKey[idempotencyKey] = claim.ClaimId;
            _claimsById[claim.ClaimId] = claim;
            return Task.FromResult(claim);
        }
    }

    public Task SaveAsync(ClaimRecord claim, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_lock)
        {
            _claimsById[claim.ClaimId] = claim;
        }

        return Task.CompletedTask;
    }

    public Task<ClaimRecord?> FindAsync(Guid claimId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_lock)
        {
            return Task.FromResult(_claimsById.GetValueOrDefault(claimId));
        }
    }
}
