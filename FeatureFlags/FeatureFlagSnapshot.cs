namespace NN.FeatureFlags;

public sealed record FeatureFlagSnapshot(
    string Version,
    IReadOnlyDictionary<string, FeatureFlagDefinition> Flags);
