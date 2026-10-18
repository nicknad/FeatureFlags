namespace NN.FeatureFlags;

public interface IFeatureFlagProvider
{
    Task<FeatureFlagSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
}
