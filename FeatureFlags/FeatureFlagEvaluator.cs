using System.Collections.Immutable;

namespace NN.FeatureFlags;

public sealed class FeatureFlagEvaluator : IFeatureFlagEvaluator
{
    private ImmutableDictionary<string, FeatureFlagDefinition> _flags =
        ImmutableDictionary.Create<string, FeatureFlagDefinition>(StringComparer.Ordinal);

    public bool IsEnabled(string key, FeatureFlagContext? context = null)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        var flags = Volatile.Read(ref _flags);

        return flags.TryGetValue(key, out var flag) && Evaluate(flag, context);
    }

    internal void Publish(IEnumerable<FeatureFlagDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        var builder = ImmutableDictionary.CreateBuilder<string, FeatureFlagDefinition>(StringComparer.Ordinal);

        foreach (var definition in definitions)
        {
            var flag = Freeze(definition);

            if (!builder.TryAdd(flag.Key, flag))
            {
                throw new InvalidOperationException($"Duplicate feature flag key: {flag.Key}");
            }
        }

        Interlocked.Exchange(ref _flags, builder.ToImmutable());
    }

    private static FeatureFlagDefinition Freeze(FeatureFlagDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.Key))
        {
            throw new InvalidOperationException("A feature flag definition has an empty key.");
        }

        var targeting = definition.Targeting;

        if (targeting is null)
        {
            return definition;
        }

        if (targeting.RolloutPercentage is { } percentage && (percentage < 0 || percentage > 100))
        {
            throw new InvalidOperationException($"Invalid rollout percentage for '{definition.Key}'.");
        }

        return definition with
        {
            Targeting = new TargetingPolicy(
                TenantAllowList: Freeze(targeting.TenantAllowList),
                UserAllowList: Freeze(targeting.UserAllowList),
                RolloutPercentage: targeting.RolloutPercentage),
        };
    }

    private static ImmutableHashSet<string>? Freeze(IReadOnlySet<string>? values)
    {
        return values is null
            ? null
            : ImmutableHashSet.CreateRange(StringComparer.Ordinal, values);
    }

    private static bool Evaluate(FeatureFlagDefinition flag, FeatureFlagContext? context)
    {
        if (!flag.IsEnabled)
        {
            return false;
        }

        var targeting = flag.Targeting;

        if (targeting is null)
        {
            return true;
        }

        if (context?.TenantId is { } tenantId &&
            targeting.TenantAllowList?.Contains(tenantId) == true)
        {
            return true;
        }

        if (context?.UserId is { } userId &&
            targeting.UserAllowList?.Contains(userId) == true)
        {
            return true;
        }

        if (targeting.RolloutPercentage is not { } percentage)
        {
            return false;
        }

        if (percentage >= 100)
        {
            return true;
        }

        if (percentage <= 0 || context?.UserId is not { } rolloutUserId)
        {
            return false;
        }

        return Rollout.Includes(flag.Key, rolloutUserId, percentage);
    }
}
