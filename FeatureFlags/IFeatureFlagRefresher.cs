namespace NN.FeatureFlags;

/// <summary>
/// Publishes feature-flag snapshots immediately, in addition to any scheduled refresh.
/// </summary>
public interface IFeatureFlagRefresher
{
    /// <summary>
    /// Refreshes the current snapshot. Concurrent callers await the same in-flight refresh.
    /// </summary>
    /// <param name="cancellationToken">
    /// Cancels waiting for the refresh. It does not cancel a refresh already started by another caller.
    /// </param>
    /// <returns>The outcome of the refresh.</returns>
    Task<FeatureFlagRefreshResult> RefreshAsync(CancellationToken cancellationToken = default);
}
