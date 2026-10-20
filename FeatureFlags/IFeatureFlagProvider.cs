namespace NN.FeatureFlags;

/// <summary>
/// Supplies complete, internally consistent feature-flag snapshots.
/// </summary>
public interface IFeatureFlagProvider
{
    /// <summary>
    /// Returns the current snapshot.
    /// </summary>
    /// <param name="cancellationToken">A token that can cancel the operation.</param>
    /// <returns>The current snapshot.</returns>
    Task<FeatureFlagSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
}
