using System;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Plugins;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.IptvHub.Services;

/// <summary>
/// Loads sources at startup and rebuilds whenever the plugin configuration is saved.
/// </summary>
public sealed class IptvStartupService : IHostedService, IDisposable
{
    private readonly IptvManager _manager;
    private readonly ILogger<IptvStartupService> _logger;
    private readonly CancellationTokenSource _stopping = new();
    private CancellationTokenSource? _debounce;

    private readonly LiveTvTagger _tagger;

    public IptvStartupService(IptvManager manager, LiveTvTagger tagger, ILogger<IptvStartupService> logger)
    {
        _manager = manager;
        _tagger = tagger;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (Plugin.Instance is null)
        {
            _logger.LogError("IPTV Hub: plugin instance not available at startup");
            return Task.CompletedTask;
        }

        Plugin.Instance.ConfigurationChanged += OnConfigurationChanged;
        _tagger.Start();

        // Give Live TV a moment to finish starting, then load from cache (downloads only if stale).
        Schedule(TimeSpan.FromSeconds(15), force: false);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (Plugin.Instance is not null)
        {
            Plugin.Instance.ConfigurationChanged -= OnConfigurationChanged;
        }

        _stopping.Cancel();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _debounce?.Dispose();
        _stopping.Dispose();
    }

    private void OnConfigurationChanged(object? sender, BasePluginConfiguration e)
        => Schedule(TimeSpan.FromSeconds(2), force: false);

    private void Schedule(TimeSpan delay, bool force)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(_stopping.Token);
        Interlocked.Exchange(ref _debounce, cts)?.Cancel();
        _ = Task.Run(
            async () =>
            {
                try
                {
                    await Task.Delay(delay, cts.Token).ConfigureAwait(false);
                    await _manager.RefreshAsync(force, _stopping.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "IPTV Hub: refresh failed");
                }
            },
            CancellationToken.None);
    }
}
