namespace NN.FeatureFlags;

public interface IFeatureFlagRefresher
{
    Task RefreshAsync(CancellationToken cancellationToken = default);
}
