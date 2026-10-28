using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.IptvHub.Services;
using MediaBrowser.Model.Tasks;

namespace Jellyfin.Plugin.IptvHub.ScheduledTasks;

/// <summary>
/// Periodically re-downloads playlists and guides.
/// </summary>
public class RefreshIptvTask : IScheduledTask
{
    private readonly IptvManager _manager;

    public RefreshIptvTask(IptvManager manager)
    {
        _manager = manager;
    }

    /// <inheritdoc />
    public string Name => "Refresh IPTV playlists and guides";

    /// <inheritdoc />
    public string Key => "IptvHubRefresh";

    /// <inheritdoc />
    public string Description => "Downloads M3U playlists and XMLTV guides configured in IPTV Hub.";

    /// <inheritdoc />
    public string Category => "Live TV";

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        progress.Report(0);
        await _manager.RefreshAsync(true, cancellationToken).ConfigureAwait(false);
        progress.Report(100);
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        var hours = Math.Max(1, Plugin.Instance?.Configuration.RefreshIntervalHours ?? 12);
        yield return new TaskTriggerInfo
        {
            Type = TaskTriggerInfoType.IntervalTrigger,
            IntervalTicks = TimeSpan.FromHours(hours).Ticks
        };
    }
}
