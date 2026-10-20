using System.Collections.Immutable;
using System.Diagnostics;

namespace NN.FeatureFlags;

public sealed class FeatureFlagEvaluator : IFeatureFlagEvaluator, IFeatureFlagSnapshotSource
{
    private static readonly FeatureFlagSnapshot EmptySnapshot = new FeatureFlagSnapshot(
        string.Empty,
        ImmutableDictionary.Create<string, FeatureFlagDefinition>(StringComparer.Ordinal));

    private readonly TaskCompletionSource<bool> _loaded =
        new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

    private FeatureFlagSnapshot _snapshot = EmptySnapshot;

    public FeatureFlagEvaluator()
    {
    }

    public FeatureFlagEvaluator(FeatureFlagSnapshot snapshot)
    {
        Guard.NotNull(snapshot, nameof(snapshot));
        Guard.NotNull(snapshot.Flags, $"{nameof(snapshot)}.{nameof(snapshot.Flags)}");

        Publish(snapshot.Version, snapshot.Flags.Values);
    }

    public event EventHandler<FeatureFlagSnapshotChangedEventArgs>? Changed;

    public bool IsLoaded => _loaded.Task.IsCompleted;

    public bool IsEnabled(string key, FeatureFlagContext? context = null)
        => EvaluateOrDefault(key, context, defaultValue: false);

    public bool IsEnabled(string key, bool defaultValue, FeatureFlagContext? context = null)
        => EvaluateOrDefault(key, context, defaultValue);

    public FeatureFlagSnapshot GetSnapshot() => Volatile.Read(ref _snapshot);

    public async Task WaitForLoadedAsync(CancellationToken cancellationToken = default)
    {
        if (IsLoaded)
        {
            return;
        }

        if (!cancellationToken.CanBeCanceled)
        {
            await _loaded.Task.ConfigureAwait(false);
            return;
        }

        var cancellation = Task.Delay(Timeout.Infinite, cancellationToken);

        await Task.WhenAny(_loaded.Task, cancellation).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();
    }

    internal bool Publish(string version, IEnumerable<FeatureFlagDefinition> definitions)
    {
        Guard.NotNull(version, nameof(version));
        Guard.NotNull(definitions, nameof(definitions));

        var current = Volatile.Read(ref _snapshot);

        if (!string.IsNullOrEmpty(version) && string.Equals(version, current.Version, StringComparison.Ordinal))
        {
            _loaded.TrySetResult(true);
            return false;
        }

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

        var snapshot = new FeatureFlagSnapshot(version, builder.ToImmutable());

        Interlocked.Exchange(ref _snapshot, snapshot);
        _loaded.TrySetResult(true);

        RaiseChanged(snapshot);

        return true;
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

    private bool EvaluateOrDefault(string key, FeatureFlagContext? context, bool defaultValue)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        var snapshot = Volatile.Read(ref _snapshot);

        return snapshot.Flags.TryGetValue(key, out var flag)
            ? Evaluate(flag, context)
            : defaultValue;
    }

    private void RaiseChanged(FeatureFlagSnapshot snapshot)
    {
        var handlers = Changed;

        if (handlers is null)
        {
            return;
        }

        var args = new FeatureFlagSnapshotChangedEventArgs(snapshot);

        foreach (var handler in handlers.GetInvocationList())
        {
            try
            {
                ((EventHandler<FeatureFlagSnapshotChangedEventArgs>)handler)(this, args);
            }
            catch (Exception exception)
            {
                Trace.TraceError($"A feature flag snapshot change subscriber failed: {exception}");
            }
        }
    }
}
