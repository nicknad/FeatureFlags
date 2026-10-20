namespace NN.FeatureFlags;

public sealed class FeatureFlagSnapshotChangedEventArgs : EventArgs
{
    public FeatureFlagSnapshotChangedEventArgs(FeatureFlagSnapshot snapshot)
    {
        Guard.NotNull(snapshot, nameof(snapshot));
        Snapshot = snapshot;
    }

    public FeatureFlagSnapshot Snapshot { get; }
}
