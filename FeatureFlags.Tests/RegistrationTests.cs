using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace NN.FeatureFlags.Tests;

public sealed class RegistrationTests
{
    [Fact]
    public void InstanceOverloadRegistersCoreServices()
    {
        var provider = new InMemoryFeatureFlagProvider();
        var services = new ServiceCollection();

        services.AddFeatureFlags(provider, enableBackgroundRefresh: false);

        using var serviceProvider = services.BuildServiceProvider();

        Assert.Same(provider, serviceProvider.GetRequiredService<IFeatureFlagProvider>());
        Assert.NotNull(serviceProvider.GetRequiredService<IFeatureFlagEvaluator>());
        Assert.NotNull(serviceProvider.GetRequiredService<IFeatureFlagRefresher>());
        Assert.NotNull(serviceProvider.GetRequiredService<IFeatureFlagSnapshotSource>());
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IHostedService));
    }

    [Fact]
    public void FactoryOverloadCallsFactoryOnce()
    {
        var calls = 0;
        var services = new ServiceCollection();

        services.AddFeatureFlags(
            _ =>
            {
                calls++;
                return new InMemoryFeatureFlagProvider();
            },
            enableBackgroundRefresh: false);

        using var serviceProvider = services.BuildServiceProvider();

        Assert.NotNull(serviceProvider.GetRequiredService<IFeatureFlagProvider>());
        Assert.Equal(1, calls);
    }

    [Fact]
    public void RepeatedRegistrationRegistersOneHostedService()
    {
        var provider = new InMemoryFeatureFlagProvider();
        var services = new ServiceCollection();

        services.AddFeatureFlags(provider);
        services.AddFeatureFlags(provider);

        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IHostedService));
    }
}
