using Microsoft.Extensions.DependencyInjection;

namespace NN.FeatureFlags;

public sealed class FeatureFlagRefresher : IFeatureFlagRefresher
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly FeatureFlagEvaluator _evaluator;
    private int _refreshing;

    public FeatureFlagRefresher(IServiceScopeFactory scopeFactory, FeatureFlagEvaluator evaluator)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(evaluator);

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

            _evaluator.Publish(snapshot.Flags.Values);
        }
        finally
        {
            Volatile.Write(ref _refreshing, 0);
        }
    }
}
