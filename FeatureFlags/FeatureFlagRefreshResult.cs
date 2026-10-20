namespace NN.FeatureFlags;

public sealed record FeatureFlagRefreshResult(
    FeatureFlagRefreshOutcome Outcome,
    string Version = "")
{
    public bool Updated => Outcome == FeatureFlagRefreshOutcome.Updated;
}
