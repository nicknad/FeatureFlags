using Microsoft.Extensions.DependencyInjection;

namespace NN.FeatureFlags.Tests;

public sealed class FeatureFlagRefresherTests
{
    [Fact]
    public async Task ConcurrentRefreshesUseOneProviderCall()
    {
        var provider = new BlockingProvider();
        using var services = FeatureFlagsTestHost.Create(provider);
        var refresher = services.GetRequiredService<IFeatureFlagRefresher>();

        var first = refresher.RefreshAsync();
        var second = refresher.RefreshAsync();

        Assert.Equal(1, provider.Calls);

        provider.Complete(Snapshot("1", Flag("flag")));

        var results = await Task.WhenAll(first, second);

        Assert.All(results, result => Assert.True(result.Updated));
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task CancelingOneWaiterDoesNotCancelTheSharedRefresh()
    {
        var provider = new BlockingProvider();
        using var services = FeatureFlagsTestHost.Create(provider);
        var refresher = services.GetRequiredService<IFeatureFlagRefresher>();

        var first = refresher.RefreshAsync();
        using var cancellation = new CancellationTokenSource();
        var second = refresher.RefreshAsync(cancellation.Token);

        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => second);

        provider.Complete(Snapshot("1", Flag("flag")));

        Assert.True((await first).Updated);
    }

    [Fact]
    public async Task HungProviderTimesOutAndLateResultIsNotPublished()
    {
        var provider = new BlockingProvider();
        using var services = FeatureFlagsTestHost.Create(provider, TimeSpan.FromMilliseconds(100));
        var evaluator = services.GetRequiredService<IFeatureFlagEvaluator>();
        var source = services.GetRequiredService<IFeatureFlagSnapshotSource>();
        var refresher = services.GetRequiredService<IFeatureFlagRefresher>();

        await Assert.ThrowsAsync<TimeoutException>(() => refresher.RefreshAsync());

        Assert.False(source.IsLoaded);

        provider.Complete(Snapshot("1", Flag("flag")));

        Assert.False(evaluator.IsEnabled("flag"));

        var next = await refresher.RefreshAsync();

        Assert.True(next.Updated);
        Assert.Equal(2, provider.Calls);
        Assert.True(evaluator.IsEnabled("flag"));
    }

    private static FeatureFlagDefinition Flag(string key) => new FeatureFlagDefinition(key, IsEnabled: true);

    private static FeatureFlagSnapshot Snapshot(string version, params FeatureFlagDefinition[] flags)
        => new FeatureFlagSnapshot(
            version,
            flags.ToDictionary(flag => flag.Key, StringComparer.Ordinal));

    private sealed class BlockingProvider : IFeatureFlagProvider
    {
        private readonly TaskCompletionSource<FeatureFlagSnapshot> _pending =
            new TaskCompletionSource<FeatureFlagSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);

        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        public Task<FeatureFlagSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _calls);

            return _pending.Task;
        }

        public void Complete(FeatureFlagSnapshot snapshot) => _pending.TrySetResult(snapshot);
    }
}
