using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NN.FeatureFlags;

public sealed partial class FeatureFlagRefreshService : BackgroundService
{
    private static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MaxDuration = TimeSpan.FromMilliseconds(int.MaxValue);

    private readonly IFeatureFlagRefresher _refresher;
    private readonly ILogger<FeatureFlagRefreshService> _logger;
    private readonly TimeSpan _interval;
    private readonly TimeSpan _timeout;

    public FeatureFlagRefreshService(
        IFeatureFlagRefresher refresher,
        IOptions<FeatureFlagOptions> options,
        ILogger<FeatureFlagRefreshService> logger)
    {
        Guard.NotNull(refresher, nameof(refresher));
        Guard.NotNull(options, nameof(options));
        Guard.NotNull(logger, nameof(logger));

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

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_interval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }

            await RefreshAsync(stoppingToken).ConfigureAwait(false);
        }
    }

    private TimeSpan Normalize(TimeSpan value, TimeSpan fallback, string name)
    {
        if (value <= TimeSpan.Zero)
        {
            LogInvalidDuration(name, fallback);
            return fallback;
        }

        if (value > MaxDuration)
        {
            LogDurationTooLarge(name, MaxDuration);
            return MaxDuration;
        }

        return value;
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            timeout.CancelAfter(_timeout);

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

    [LoggerMessage(EventId = 5, Level = LogLevel.Warning, Message = "Feature flag option '{Name}' exceeds the maximum of {Maximum}; using {Maximum}.")]
    private partial void LogDurationTooLarge(string name, TimeSpan maximum);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "Feature flag refresh exceeded the {Timeout} timeout.")]
    private partial void LogRefreshTimedOut(TimeSpan timeout);

    [LoggerMessage(EventId = 4, Level = LogLevel.Debug, Message = "Feature flag refresh canceled.")]
    private partial void LogRefreshCanceled();
}
