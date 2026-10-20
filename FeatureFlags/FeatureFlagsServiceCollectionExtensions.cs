using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace NN.FeatureFlags;

public static class FeatureFlagsServiceCollectionExtensions
{
    public static IServiceCollection AddFeatureFlags<TProvider>(
        this IServiceCollection services,
        Action<FeatureFlagOptions>? configure = null)
        where TProvider : class, IFeatureFlagProvider
        => AddFeatureFlags<TProvider>(services, configure, enableBackgroundRefresh: true);

    public static IServiceCollection AddFeatureFlags<TProvider>(
        this IServiceCollection services,
        Action<FeatureFlagOptions>? configure,
        bool enableBackgroundRefresh)
        where TProvider : class, IFeatureFlagProvider
    {
        Guard.NotNull(services, nameof(services));

        services.TryAddSingleton<TProvider>();
        services.TryAddSingleton<IFeatureFlagProvider>(static sp => sp.GetRequiredService<TProvider>());

        return AddFeatureFlagsCore(services, configure, enableBackgroundRefresh);
    }

    public static IServiceCollection AddFeatureFlags(
        this IServiceCollection services,
        Func<IServiceProvider, IFeatureFlagProvider> providerFactory,
        Action<FeatureFlagOptions>? configure = null,
        bool enableBackgroundRefresh = true)
    {
        Guard.NotNull(services, nameof(services));
        Guard.NotNull(providerFactory, nameof(providerFactory));

        services.TryAddSingleton<IFeatureFlagProvider>(providerFactory);

        return AddFeatureFlagsCore(services, configure, enableBackgroundRefresh);
    }

    public static IServiceCollection AddFeatureFlags(
        this IServiceCollection services,
        IFeatureFlagProvider provider,
        Action<FeatureFlagOptions>? configure = null,
        bool enableBackgroundRefresh = true)
    {
        Guard.NotNull(services, nameof(services));
        Guard.NotNull(provider, nameof(provider));

        services.TryAddSingleton(provider);

        return AddFeatureFlagsCore(services, configure, enableBackgroundRefresh);
    }

    private static IServiceCollection AddFeatureFlagsCore(
        IServiceCollection services,
        Action<FeatureFlagOptions>? configure,
        bool enableBackgroundRefresh)
    {
        services.AddOptions<FeatureFlagOptions>()
            .Validate(
                static options => options.RefreshInterval > TimeSpan.Zero,
                "FeatureFlagOptions.RefreshInterval must be positive.")
            .Validate(
                static options => options.RefreshTimeout > TimeSpan.Zero,
                "FeatureFlagOptions.RefreshTimeout must be positive.")
            .ValidateOnStart();

        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.TryAddSingleton<FeatureFlagEvaluator>();
        services.TryAddSingleton<IFeatureFlagEvaluator>(static sp => sp.GetRequiredService<FeatureFlagEvaluator>());
        services.TryAddSingleton<IFeatureFlagSnapshotSource>(static sp => sp.GetRequiredService<FeatureFlagEvaluator>());
        services.TryAddSingleton<IFeatureFlagRefresher, FeatureFlagRefresher>();

        if (enableBackgroundRefresh)
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, FeatureFlagRefreshService>());
        }

        return services;
    }
}
