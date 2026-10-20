using System.Collections.Immutable;

namespace NN.FeatureFlags;

public sealed record FeatureFlagContext(
    string? UserId = null,
    string? TenantId = null)
{
    private readonly IReadOnlyDictionary<string, string> _attributes =
        ImmutableDictionary<string, string>.Empty;

    /// <summary>
    /// Stable identity used for percentage bucketing. Defaults to <see cref="UserId"/> when not set.
    /// </summary>
    public string? TargetingKey { get; init; }

    /// <summary>
    /// Additional subject attributes available to targeting rules.
    /// </summary>
    public IReadOnlyDictionary<string, string> Attributes
    {
        get => _attributes;
        init => _attributes = value is null
            ? ImmutableDictionary<string, string>.Empty
            : ImmutableDictionary.CreateRange(StringComparer.Ordinal, value);
    }
}
