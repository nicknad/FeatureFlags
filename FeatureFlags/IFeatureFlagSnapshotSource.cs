namespace NN.FeatureFlags;

public interface IFeatureFlagSnapshotSource
{
    event EventHandler<FeatureFlagSnapshotChangedEventArgs>? Changed;

    FeatureFlagSnapshot GetSnapshot();
}
