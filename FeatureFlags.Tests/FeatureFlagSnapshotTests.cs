using Microsoft.Extensions.DependencyInjection;

namespace NN.FeatureFlags.Tests;

public sealed class FeatureFlagSnapshotTests
{
    [Fact]
    public async Task FirstRefreshPublishesSnapshot()
    {
        var provider = new InMemoryFeatureFlagProvider(Snapshot("1", Flag("flag")));
        using var services = FeatureFlagsTestHost.Create(provider);
        var evaluator = services.GetRequiredService<IFeatureFlagEvaluator>();
        var source = services.GetRequiredService<IFeatureFlagSnapshotSource>();
        var refresher = services.GetRequiredService<IFeatureFlagRefresher>();

        Assert.False(source.IsLoaded);

        var result = await refresher.RefreshAsync();

        Assert.True(result.Updated);
        Assert.Equal(FeatureFlagRefreshOutcome.Updated, result.Outcome);
        Assert.Equal("1", result.Version);
        Assert.True(source.IsLoaded);
        Assert.True(evaluator.IsEnabled("flag"));
        Assert.Equal("1", source.GetSnapshot().Version);
    }

    [Fact]
    public async Task SameVersionIsUnchangedAndDoesNotRaiseChanged()
    {
        var provider = new InMemoryFeatureFlagProvider(Snapshot("1", Flag("flag")));
        using var services = FeatureFlagsTestHost.Create(provider);
        var source = services.GetRequiredService<IFeatureFlagSnapshotSource>();
        var refresher = services.GetRequiredService<IFeatureFlagRefresher>();
        var changes = 0;

        source.Changed += (_, _) => changes++;

        Assert.True((await refresher.RefreshAsync()).Updated);
        Assert.Equal(1, changes);

        var second = await refresher.RefreshAsync();

        Assert.Equal(FeatureFlagRefreshOutcome.Unchanged, second.Outcome);
        Assert.Equal(1, changes);
    }

    [Fact]
    public async Task NewVersionPublishesAndRaisesChanged()
    {
        var provider = new InMemoryFeatureFlagProvider(Snapshot("1", Flag("flag")));
        using var services = FeatureFlagsTestHost.Create(provider);
        var evaluator = services.GetRequiredService<IFeatureFlagEvaluator>();
        var source = services.GetRequiredService<IFeatureFlagSnapshotSource>();
        var refresher = services.GetRequiredService<IFeatureFlagRefresher>();
        var changes = 0;

        source.Changed += (_, _) => changes++;

        await refresher.RefreshAsync();

        provider.Snapshot = Snapshot("2", Flag("flag"), Flag("second"));

        var result = await refresher.RefreshAsync();

        Assert.True(result.Updated);
        Assert.Equal(2, changes);
        Assert.True(evaluator.IsEnabled("second"));
        Assert.Equal("2", source.GetSnapshot().Version);
    }

    [Fact]
    public async Task InvalidSnapshotRetainsLastGood()
    {
        var provider = new InMemoryFeatureFlagProvider(Snapshot("1", Flag("flag")));
        using var services = FeatureFlagsTestHost.Create(provider);
        var evaluator = services.GetRequiredService<IFeatureFlagEvaluator>();
        var source = services.GetRequiredService<IFeatureFlagSnapshotSource>();
        var refresher = services.GetRequiredService<IFeatureFlagRefresher>();

        await refresher.RefreshAsync();

        provider.Snapshot = Snapshot(
            "2",
            new FeatureFlagDefinition(
                "broken",
                IsEnabled: true,
                new TargetingPolicy(RolloutPercentage: 101)));

        await Assert.ThrowsAsync<InvalidOperationException>(() => refresher.RefreshAsync());

        Assert.Equal("1", source.GetSnapshot().Version);
        Assert.True(evaluator.IsEnabled("flag"));
        Assert.False(evaluator.IsEnabled("broken"));
    }

    [Fact]
    public async Task WaitForLoadedCompletesAfterRefresh()
    {
        var provider = new InMemoryFeatureFlagProvider(Snapshot("1", Flag("flag")));
        using var services = FeatureFlagsTestHost.Create(provider);
        var source = services.GetRequiredService<IFeatureFlagSnapshotSource>();
        var refresher = services.GetRequiredService<IFeatureFlagRefresher>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var waiting = source.WaitForLoadedAsync(timeout.Token);

        Assert.False(waiting.IsCompleted);

        await refresher.RefreshAsync();
        await waiting;
    }

    [Fact]
    public async Task WaitForLoadedHonorsCancellation()
    {
        var evaluator = new FeatureFlagEvaluator();
        using var cancellation = new CancellationTokenSource();

        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => evaluator.WaitForLoadedAsync(cancellation.Token));
    }

    [Fact]
    public async Task InMemoryProviderReturnsCurrentSnapshot()
    {
        var provider = new InMemoryFeatureFlagProvider();

        Assert.Empty(provider.Snapshot.Flags);

        var snapshot = Snapshot("v", Flag("flag"));
        provider.Snapshot = snapshot;

        Assert.Same(snapshot, await provider.GetSnapshotAsync());
    }

    [Fact]
    public async Task InMemoryProviderHonorsCancellation()
    {
        var provider = new InMemoryFeatureFlagProvider();
        using var cancellation = new CancellationTokenSource();

        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => provider.GetSnapshotAsync(cancellation.Token));
    }

    private static FeatureFlagDefinition Flag(string key) => new FeatureFlagDefinition(key, IsEnabled: true);

    private static FeatureFlagSnapshot Snapshot(string version, params FeatureFlagDefinition[] flags)
        => new FeatureFlagSnapshot(
            version,
            flags.ToDictionary(flag => flag.Key, StringComparer.Ordinal));
}
