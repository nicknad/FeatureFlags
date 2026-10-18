using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace NN.FeatureFlags;

public static class FeatureFlagsServiceCollectionExtensions
{
    public static IServiceCollection AddFeatureFlags<TProvider>(
        this IServiceCollection services,
        Action<FeatureFlagOptions>? configure = null)
        where TProvider : class, IFeatureFlagProvider
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<FeatureFlagOptions>();

        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.TryAddSingleton<TProvider>();
        services.TryAddSingleton<IFeatureFlagProvider>(static sp => sp.GetRequiredService<TProvider>());
        services.TryAddSingleton<FeatureFlagEvaluator>();
        services.TryAddSingleton<IFeatureFlagEvaluator>(static sp => sp.GetRequiredService<FeatureFlagEvaluator>());
        services.TryAddSingleton<IFeatureFlagRefresher, FeatureFlagRefresher>();
        services.AddHostedService<FeatureFlagRefreshService>();

        return services;
    }
}
