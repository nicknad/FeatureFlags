namespace NN.FeatureFlags;

/// <summary>
/// Defines a feature flag and its targeting policy.
/// </summary>
/// <param name="Key">The unique, case-sensitive flag key.</param>
/// <param name="IsEnabled">The master switch. When <see langword="false"/> the flag is off for everyone.</param>
/// <param name="Targeting">The targeting policy, or <see langword="null"/> for unrestricted access.</param>
public sealed record FeatureFlagDefinition(
    string Key,
    bool IsEnabled,
    TargetingPolicy? Targeting = null);
