namespace NN.FeatureFlags;

public interface IFeatureFlagSnapshotSource
{
    event EventHandler<FeatureFlagSnapshotChangedEventArgs>? Changed;

    bool IsLoaded { get; }

    FeatureFlagSnapshot GetSnapshot();

    Task WaitForLoadedAsync(CancellationToken cancellationToken = default);
}
