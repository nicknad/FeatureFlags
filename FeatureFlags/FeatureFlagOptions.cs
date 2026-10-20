namespace NN.FeatureFlags;

/// <summary>
/// Configures feature-flag synchronization.
/// </summary>
public sealed class FeatureFlagOptions
{
    /// <summary>
    /// Gets or sets the interval between background refreshes. Defaults to 120 seconds.
    /// </summary>
    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromSeconds(120);

    /// <summary>
    /// Gets or sets the maximum duration of a provider call. Defaults to 30 seconds.
    /// </summary>
    public TimeSpan RefreshTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
