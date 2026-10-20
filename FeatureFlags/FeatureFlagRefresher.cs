using Microsoft.Extensions.DependencyInjection;

namespace NN.FeatureFlags;

public sealed class FeatureFlagRefresher : IFeatureFlagRefresher
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly FeatureFlagEvaluator _evaluator;
    private int _refreshing;

    public FeatureFlagRefresher(IServiceScopeFactory scopeFactory, FeatureFlagEvaluator evaluator)
    {
        Guard.NotNull(scopeFactory, nameof(scopeFactory));
        Guard.NotNull(evaluator, nameof(evaluator));

        _scopeFactory = scopeFactory;
        _evaluator = evaluator;
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _refreshing, 1, 0) != 0)
        {
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var provider = scope.ServiceProvider.GetRequiredService<IFeatureFlagProvider>();
            var snapshot = await provider.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

            Guard.NotNull(snapshot, nameof(snapshot));
            Guard.NotNull(snapshot.Flags, $"{nameof(snapshot)}.{nameof(snapshot.Flags)}");

            _evaluator.Publish(snapshot.Version, snapshot.Flags.Values);
        }
        finally
        {
            Volatile.Write(ref _refreshing, 0);
        }
    }
}
