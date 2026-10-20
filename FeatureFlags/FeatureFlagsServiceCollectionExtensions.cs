using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace NN.FeatureFlags;

/// <summary>
/// Registers feature-flag evaluation and synchronization services.
/// </summary>
public static class FeatureFlagsServiceCollectionExtensions
{
    /// <summary>
    /// Registers a provider type, the evaluator, the refresher, and (optionally) the background refresh service.
    /// </summary>
    /// <typeparam name="TProvider">The provider type. It is resolved from a new scope on every refresh.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">An optional configuration callback for <see cref="FeatureFlagOptions"/>.</param>
    /// <param name="enableBackgroundRefresh">
    /// Whether to register the hosted service that loads flags at startup and refreshes on an interval.
    /// </param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddFeatureFlags<TProvider>(
        this IServiceCollection services,
        Action<FeatureFlagOptions>? configure = null,
        bool enableBackgroundRefresh = true)
        where TProvider : class, IFeatureFlagProvider
    {
        Guard.NotNull(services, nameof(services));

        services.TryAddScoped<TProvider>();
        services.TryAddScoped<IFeatureFlagProvider>(static sp => sp.GetRequiredService<TProvider>());

        return AddFeatureFlagsCore(services, configure, enableBackgroundRefresh);
    }

    /// <summary>
    /// Registers a provider factory, the evaluator, the refresher, and (optionally) the background refresh service.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="providerFactory">The factory used to create a provider for each refresh.</param>
    /// <param name="configure">An optional configuration callback for <see cref="FeatureFlagOptions"/>.</param>
    /// <param name="enableBackgroundRefresh">
    /// Whether to register the hosted service that loads flags at startup and refreshes on an interval.
    /// </param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddFeatureFlags(
        this IServiceCollection services,
        Func<IServiceProvider, IFeatureFlagProvider> providerFactory,
        Action<FeatureFlagOptions>? configure = null,
        bool enableBackgroundRefresh = true)
    {
        Guard.NotNull(services, nameof(services));
        Guard.NotNull(providerFactory, nameof(providerFactory));

        services.TryAddScoped<IFeatureFlagProvider>(providerFactory);

        return AddFeatureFlagsCore(services, configure, enableBackgroundRefresh);
    }

    /// <summary>
    /// Registers a provider instance, the evaluator, the refresher, and (optionally) the background refresh service.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="provider">The provider instance.</param>
    /// <param name="configure">An optional configuration callback for <see cref="FeatureFlagOptions"/>.</param>
    /// <param name="enableBackgroundRefresh">
    /// Whether to register the hosted service that loads flags at startup and refreshes on an interval.
    /// </param>
    /// <returns>The service collection.</returns>
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
