namespace NN.FeatureFlags;

/// <summary>
/// Describes the result of a refresh request.
/// </summary>
/// <param name="Outcome">The outcome of the refresh.</param>
/// <param name="Version">The snapshot version reported by the provider.</param>
public sealed record FeatureFlagRefreshResult(
    FeatureFlagRefreshOutcome Outcome,
    string Version = "")
{
    /// <summary>
    /// Gets a value indicating whether a new snapshot was published.
    /// </summary>
    public bool Updated => Outcome == FeatureFlagRefreshOutcome.Updated;
}
