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
        Guard.NotNull(definitions, nameof(definitions));

        var builder = ImmutableDictionary.CreateBuilder<string, FeatureFlagDefinition>(StringComparer.Ordinal);

        foreach (var definition in definitions)
        {
            var flag = Freeze(definition);

            if (builder.ContainsKey(flag.Key))
            {
                throw new InvalidOperationException($"Duplicate feature flag key: {flag.Key}");
            }

            builder.Add(flag.Key, flag);
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

    private static ImmutableHashSet<string>? Freeze(IReadOnlyCollection<string>? values)
    {
        if (values is null)
        {
            return null;
        }

        var builder = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);

        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException("A targeting allow-list contains a null or empty value.");
            }

            builder.Add(value);
        }

        return builder.ToImmutable();
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
            Contains(targeting.TenantAllowList, tenantId))
        {
            return true;
        }

        if (context?.UserId is { } userId &&
            Contains(targeting.UserAllowList, userId))
        {
            return true;
        }

        if (targeting.RolloutPercentage is not { } percentage || percentage <= 0)
        {
            return false;
        }

        if (percentage >= 100)
        {
            return true;
        }

        var targetingKey = GetTargetingKey(context);

        return targetingKey is not null && Rollout.Includes(flag.Key, targetingKey, percentage);
    }

    private static string? GetTargetingKey(FeatureFlagContext? context)
    {
        if (context is null)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(context.TargetingKey)
            ? context.UserId
            : context.TargetingKey;
    }

    private static bool Contains(IReadOnlyCollection<string>? values, string value)
    {
        if (values is null)
        {
            return false;
        }

        return values is ImmutableHashSet<string> set
            ? set.Contains(value)
            : values.Contains(value, StringComparer.Ordinal);
    }
}
