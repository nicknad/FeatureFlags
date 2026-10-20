namespace NN.FeatureFlags;

public interface IFeatureFlagEvaluator
{
    bool IsEnabled(string key, FeatureFlagContext? context = null);

    bool IsEnabled(string key, bool defaultValue, FeatureFlagContext? context = null);
}
