using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace NN.FeatureFlags;

public sealed class FeatureFlagRefresher : IFeatureFlagRefresher
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MaxTimeout = TimeSpan.FromMilliseconds(int.MaxValue);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly FeatureFlagEvaluator _evaluator;
    private readonly object _gate = new object();
    private readonly TimeSpan _timeout;

    private Task<FeatureFlagRefreshResult>? _inFlight;
    private long _generation;

    public FeatureFlagRefresher(
        IServiceScopeFactory scopeFactory,
        FeatureFlagEvaluator evaluator,
        IOptions<FeatureFlagOptions> options)
    {
        Guard.NotNull(scopeFactory, nameof(scopeFactory));
        Guard.NotNull(evaluator, nameof(evaluator));
        Guard.NotNull(options, nameof(options));

        _scopeFactory = scopeFactory;
        _evaluator = evaluator;
        _timeout = NormalizeTimeout(options.Value.RefreshTimeout);
    }

    public Task<FeatureFlagRefreshResult> RefreshAsync(CancellationToken cancellationToken = default)
    {
        Task<FeatureFlagRefreshResult> refresh;

        lock (_gate)
        {
            refresh = _inFlight ??= RefreshCoreAsync(Interlocked.Increment(ref _generation));
        }

        return WaitAsync(refresh, cancellationToken);
    }

    private static TimeSpan NormalizeTimeout(TimeSpan value)
    {
        if (value <= TimeSpan.Zero)
        {
            return DefaultTimeout;
        }

        return value > MaxTimeout ? MaxTimeout : value;
    }

    private static async Task<FeatureFlagRefreshResult> WaitAsync(
        Task<FeatureFlagRefreshResult> refresh,
        CancellationToken cancellationToken)
    {
        if (!cancellationToken.CanBeCanceled)
        {
            return await refresh.ConfigureAwait(false);
        }

        var cancellation = Task.Delay(Timeout.Infinite, cancellationToken);
        var completed = await Task.WhenAny(refresh, cancellation).ConfigureAwait(false);

        if (completed != refresh)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }

        return await refresh.ConfigureAwait(false);
    }

    private static void Observe(Task task)
    {
        _ = task.ContinueWith(
            static completed => _ = completed.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private async Task<FeatureFlagRefreshResult> RefreshCoreAsync(long generation)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var provider = scope.ServiceProvider.GetRequiredService<IFeatureFlagProvider>();

            var snapshot = await LoadAsync(provider, generation).ConfigureAwait(false);

            Guard.NotNull(snapshot, nameof(snapshot));
            Guard.NotNull(snapshot.Flags, $"{nameof(snapshot)}.{nameof(snapshot.Flags)}");

            if (Interlocked.Read(ref _generation) != generation)
            {
                return new FeatureFlagRefreshResult(FeatureFlagRefreshOutcome.Superseded, snapshot.Version);
            }

            var updated = _evaluator.Publish(snapshot.Version, snapshot.Flags.Values);

            return new FeatureFlagRefreshResult(
                updated ? FeatureFlagRefreshOutcome.Updated : FeatureFlagRefreshOutcome.Unchanged,
                snapshot.Version);
        }
        finally
        {
            lock (_gate)
            {
                if (Interlocked.Read(ref _generation) == generation)
                {
                    _inFlight = null;
                }
            }
        }
    }

    private async Task<FeatureFlagSnapshot> LoadAsync(IFeatureFlagProvider provider, long generation)
    {
        var load = provider.GetSnapshotAsync(CancellationToken.None);
        var timeout = Task.Delay(_timeout);

        var completed = await Task.WhenAny(load, timeout).ConfigureAwait(false);

        if (completed == load)
        {
            return await load.ConfigureAwait(false);
        }

        Abandon(generation);
        Observe(load);

        throw new TimeoutException($"Feature flag refresh did not complete within {_timeout}.");
    }

    private void Abandon(long generation)
    {
        lock (_gate)
        {
            if (Interlocked.Read(ref _generation) != generation)
            {
                return;
            }

            Interlocked.Increment(ref _generation);
            _inFlight = null;
        }
    }
}
