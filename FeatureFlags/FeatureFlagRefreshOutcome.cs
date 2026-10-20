namespace NN.FeatureFlags;

/// <summary>
/// Describes the outcome of a refresh request.
/// </summary>
public enum FeatureFlagRefreshOutcome
{
    /// <summary>
    /// A new snapshot was published.
    /// </summary>
    Updated,

    /// <summary>
    /// The provider reported a version that is already published.
    /// </summary>
    Unchanged,

    /// <summary>
    /// A newer refresh took over after this one timed out.
    /// </summary>
    Superseded,
}
