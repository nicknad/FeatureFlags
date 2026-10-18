namespace NN.FeatureFlags;

public sealed class FeatureFlagOptions
{
    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromSeconds(120);

    public TimeSpan RefreshTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
