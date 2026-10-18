using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NN.FeatureFlags;

public sealed partial class FeatureFlagRefreshService : BackgroundService
{
    private static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    private readonly IFeatureFlagRefresher _refresher;
    private readonly ILogger<FeatureFlagRefreshService> _logger;
    private readonly TimeSpan _interval;
    private readonly TimeSpan _timeout;

    public FeatureFlagRefreshService(
        IFeatureFlagRefresher refresher,
        IOptions<FeatureFlagOptions> options,
        ILogger<FeatureFlagRefreshService> logger)
    {
        ArgumentNullException.ThrowIfNull(refresher);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _refresher = refresher;
        _logger = logger;

        var configuration = options.Value;
        _interval = Normalize(configuration.RefreshInterval, DefaultInterval, nameof(FeatureFlagOptions.RefreshInterval));
        _timeout = Normalize(configuration.RefreshTimeout, DefaultTimeout, nameof(FeatureFlagOptions.RefreshTimeout));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        await RefreshAsync(stoppingToken).ConfigureAwait(false);

        using var timer = new PeriodicTimer(_interval);

        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            await RefreshAsync(stoppingToken).ConfigureAwait(false);
        }
    }

    private TimeSpan Normalize(TimeSpan value, TimeSpan fallback, string name)
    {
        if (value > TimeSpan.Zero)
        {
            return value;
        }

        LogInvalidDuration(name, fallback);
        return fallback;
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_timeout);

        try
        {
            await _refresher.RefreshAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            LogRefreshCanceled();
        }
        catch (OperationCanceledException)
        {
            LogRefreshTimedOut(_timeout);
        }
        catch (Exception exception)
        {
            LogRefreshFailed(exception);
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Failed to refresh feature flag configuration.")]
    private partial void LogRefreshFailed(Exception exception);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Feature flag option '{Name}' must be positive; using {Fallback}.")]
    private partial void LogInvalidDuration(string name, TimeSpan fallback);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "Feature flag refresh exceeded the {Timeout} timeout.")]
    private partial void LogRefreshTimedOut(TimeSpan timeout);

    [LoggerMessage(EventId = 4, Level = LogLevel.Debug, Message = "Feature flag refresh canceled.")]
    private partial void LogRefreshCanceled();
}
