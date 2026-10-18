namespace NN.FeatureFlags;

public interface IFeatureFlagEvaluator
{
    bool IsEnabled(string key, FeatureFlagContext? context = null);
}
