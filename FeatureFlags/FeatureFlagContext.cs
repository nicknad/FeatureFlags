namespace NN.FeatureFlags;

public sealed record FeatureFlagContext(
    string? UserId = null,
    string? TenantId = null);
