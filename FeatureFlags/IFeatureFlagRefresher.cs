namespace NN.FeatureFlags;

public interface IFeatureFlagRefresher
{
    Task<FeatureFlagRefreshResult> RefreshAsync(CancellationToken cancellationToken = default);
}
