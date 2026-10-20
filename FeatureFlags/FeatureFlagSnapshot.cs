namespace NN.FeatureFlags;

/// <summary>
/// An immutable view of all feature-flag definitions at a point in time.
/// </summary>
/// <param name="Version">
/// Identifies the snapshot. When non-empty, a refresh that reports the same version is treated as unchanged.
/// </param>
/// <param name="Flags">The flag definitions, keyed by <see cref="FeatureFlagDefinition.Key"/>.</param>
public sealed record FeatureFlagSnapshot(
    string Version,
    IReadOnlyDictionary<string, FeatureFlagDefinition> Flags);
