namespace NN.FeatureFlags;

public sealed record FeatureFlagDefinition(
    string Key,
    bool IsEnabled,
    TargetingPolicy? Targeting = null);
