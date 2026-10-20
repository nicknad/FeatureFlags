using System.Collections.Immutable;

namespace NN.FeatureFlags;

/// <summary>
/// Describes the subject of a feature-flag evaluation.
/// </summary>
/// <param name="UserId">The user identifier, used by user allow-lists and as the default targeting key.</param>
/// <param name="TenantId">The tenant identifier, used by tenant allow-lists.</param>
public sealed record FeatureFlagContext(
    string? UserId = null,
    string? TenantId = null)
{
    private readonly IReadOnlyDictionary<string, string> _attributes =
        ImmutableDictionary<string, string>.Empty;

    /// <summary>
    /// Gets or sets a stable identity used for percentage bucketing.
    /// When empty, evaluation falls back to <see cref="UserId"/>.
    /// </summary>
    public string? TargetingKey { get; init; }

    /// <summary>
    /// Gets or sets additional subject attributes. The dictionary is copied on assignment.
    /// </summary>
    public IReadOnlyDictionary<string, string> Attributes
    {
        get => _attributes;
        init => _attributes = value is null
            ? ImmutableDictionary<string, string>.Empty
            : ImmutableDictionary.CreateRange(StringComparer.Ordinal, value);
    }
}
