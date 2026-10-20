namespace NN.FeatureFlags;

/// <summary>
/// Provides data for <see cref="IFeatureFlagSnapshotSource.Changed"/>.
/// </summary>
public sealed class FeatureFlagSnapshotChangedEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FeatureFlagSnapshotChangedEventArgs"/> class.
    /// </summary>
    /// <param name="snapshot">The snapshot that was published.</param>
    public FeatureFlagSnapshotChangedEventArgs(FeatureFlagSnapshot snapshot)
    {
        Guard.NotNull(snapshot, nameof(snapshot));
        Snapshot = snapshot;
    }

    /// <summary>
    /// Gets the snapshot that was published.
    /// </summary>
    public FeatureFlagSnapshot Snapshot { get; }
}
