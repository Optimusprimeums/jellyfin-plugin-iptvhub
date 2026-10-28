using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.IptvHub.Models;
using MediaBrowser.Common.Extensions;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.IptvHub.Services;

/// <summary>
/// Tags the Live TV channels created from the plugin's playlists with their categories, so they can be
/// found by category in Jellyfin (tag links on a channel's page, and the /Items?tags= filter).
/// </summary>
public sealed class LiveTvTagger : IDisposable
{
    private readonly IptvManager _manager;
    private readonly ILibraryManager _libraryManager;
    private readonly ITaskManager _taskManager;
    private readonly ILogger<LiveTvTagger> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public LiveTvTagger(IptvManager manager, ILibraryManager libraryManager, ITaskManager taskManager, ILogger<LiveTvTagger> logger)
    {
        _manager = manager;
        _libraryManager = libraryManager;
        _taskManager = taskManager;
        _logger = logger;
    }

    /// <summary>Starts listening for refreshes.</summary>
    public void Start()
    {
        _manager.Refreshed += OnRefreshed;
        _taskManager.TaskCompleted += OnTaskCompleted;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _manager.Refreshed -= OnRefreshed;
        _taskManager.TaskCompleted -= OnTaskCompleted;
        _lock.Dispose();
    }

    /// <summary>
    /// Brings the tags of every plugin-created Live TV channel in line with its categories.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of channels updated.</returns>
    public async Task<int> TagChannelsAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var snapshot = _manager.Snapshot;
            if (snapshot.Channels.Count == 0)
            {
                return 0;
            }

            var items = _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.TvChannel },
                Recursive = true
            });

            // Index our visible channels by the id Jellyfin's M3U tuner gives them; the first one wins for
            // duplicate stream URLs, just as Jellyfin keeps only one channel for them.
            var byExternalId = new Dictionary<string, IptvChannel>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in snapshot.Channels.Where(c => !c.Hidden))
            {
                byExternalId.TryAdd(ExternalIdFor(IptvManager.PlaylistPath(c.SourceId), c.Url), c);
            }

            var updated = 0;
            var matched = 0;
            var liveTvChannels = items.OfType<LiveTvChannel>().ToList();
            foreach (var channel in liveTvChannels)
            {
                if (string.IsNullOrEmpty(channel.ExternalId) || !byExternalId.TryGetValue(channel.ExternalId, out var ours))
                {
                    continue;
                }

                matched++;

                var tags = IptvManager.CategoryNamesFor(ours, snapshot.VisibleCategoryNames);
                if (tags.Length == 0)
                {
                    tags = new[] { ours.Group };
                }

                if (channel.Tags.SequenceEqual(tags, StringComparer.Ordinal))
                {
                    continue;
                }

                channel.Tags = tags;
                await channel.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, cancellationToken).ConfigureAwait(false);
                updated++;
            }

            _logger.LogInformation(
                "IPTV Hub: {Matched} of {Total} Live TV channels are from IPTV Hub; updated category tags on {Updated}",
                matched,
                liveTvChannels.Count,
                updated);

            return updated;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// The external id Jellyfin's M3U tuner assigns to a channel: "m3u_" + MD5(tuner playlist path) + MD5(stream URL),
    /// using Jellyfin's own hashing so the two always agree.
    /// </summary>
    /// <param name="tunerUrl">The tuner's playlist path or URL.</param>
    /// <param name="streamUrl">The channel's stream URL.</param>
    /// <returns>The external id.</returns>
    public static string ExternalIdFor(string tunerUrl, string streamUrl)
        => "m3u_"
            + tunerUrl.GetMD5().ToString("N", CultureInfo.InvariantCulture)
            + streamUrl.Trim().GetMD5().ToString("N", CultureInfo.InvariantCulture);

    private void OnRefreshed(object? sender, EventArgs e) => Run();

    private void OnTaskCompleted(object? sender, TaskCompletionEventArgs e)
    {
        if (e.Task?.ScheduledTask?.Key == "RefreshGuide")
        {
            Run();
        }
    }

    private void Run()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await TagChannelsAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "IPTV Hub: failed to tag Live TV channels");
            }
        });
    }
}
