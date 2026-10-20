namespace NN.FeatureFlags;

/// <summary>
/// Provides read access to the currently published feature-flag snapshot and its load state.
/// </summary>
public interface IFeatureFlagSnapshotSource
{
    /// <summary>
    /// Occurs after a new snapshot is published. A throwing subscriber does not fail the refresh.
    /// </summary>
    event EventHandler<FeatureFlagSnapshotChangedEventArgs>? Changed;

    /// <summary>
    /// Gets a value indicating whether a snapshot has ever been published.
    /// </summary>
    bool IsLoaded { get; }

    /// <summary>
    /// Gets the currently published snapshot, or an empty snapshot before the first successful refresh.
    /// </summary>
    /// <returns>The currently published snapshot.</returns>
    FeatureFlagSnapshot GetSnapshot();

    /// <summary>
    /// Completes when the first snapshot is published.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the wait.</param>
    /// <returns>A task that completes on the first publication.</returns>
    Task WaitForLoadedAsync(CancellationToken cancellationToken = default);
}
