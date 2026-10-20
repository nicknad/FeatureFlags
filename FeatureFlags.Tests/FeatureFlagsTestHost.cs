using Microsoft.Extensions.DependencyInjection;

namespace NN.FeatureFlags.Tests;

internal static class FeatureFlagsTestHost
{
    public static ServiceProvider Create(IFeatureFlagProvider provider, TimeSpan? refreshTimeout = null)
    {
        var services = new ServiceCollection();

        services.AddFeatureFlags(
            provider,
            options => options.RefreshTimeout = refreshTimeout ?? TimeSpan.FromSeconds(5),
            enableBackgroundRefresh: false);

        return services.BuildServiceProvider();
    }
}
