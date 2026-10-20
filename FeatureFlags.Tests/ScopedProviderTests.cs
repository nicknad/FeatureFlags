using Microsoft.Extensions.DependencyInjection;

namespace NN.FeatureFlags.Tests;

public sealed class ScopedProviderTests
{
    [Fact]
    public async Task ScopedProviderResolvesScopedDependenciesPerRefresh()
    {
        var probe = new Probe();
        var services = new ServiceCollection();

        services.AddSingleton(probe);
        services.AddScoped(_ =>
        {
            probe.Dependencies++;
            return new ScopedDependency();
        });
        services.AddFeatureFlags<ScopedProvider>(enableBackgroundRefresh: false);

        using var serviceProvider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true });

        var evaluator = serviceProvider.GetRequiredService<IFeatureFlagEvaluator>();
        var refresher = serviceProvider.GetRequiredService<IFeatureFlagRefresher>();

        Assert.True((await refresher.RefreshAsync()).Updated);
        Assert.True(evaluator.IsEnabled("scoped"));
        Assert.Equal(1, probe.Providers);
        Assert.Equal(1, probe.Dependencies);
    }

    private sealed class Probe
    {
        public int Providers { get; set; }

        public int Dependencies { get; set; }
    }

    private sealed class ScopedDependency
    {
    }

    private sealed class ScopedProvider : IFeatureFlagProvider
    {
        public ScopedProvider(Probe probe, ScopedDependency dependency)
        {
            ArgumentNullException.ThrowIfNull(dependency);

            probe.Providers++;
        }

        public Task<FeatureFlagSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var flags = new Dictionary<string, FeatureFlagDefinition>(StringComparer.Ordinal)
            {
                ["scoped"] = new FeatureFlagDefinition("scoped", IsEnabled: true),
            };

            return Task.FromResult(new FeatureFlagSnapshot("1", flags));
        }
    }
}
