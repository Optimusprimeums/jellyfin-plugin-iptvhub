using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
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

            var updated = 0;
            foreach (var channel in items.OfType<LiveTvChannel>())
            {
                var key = KeyFromExternalId(channel.ExternalId);
                if (key is null || !snapshot.ByKey.TryGetValue(key, out var ours))
                {
                    continue;
                }

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

            if (updated > 0)
            {
                _logger.LogInformation("IPTV Hub: tagged {Count} Live TV channels with their categories", updated);
            }

            return updated;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Extracts the plugin's channel key from a Live TV channel's external id ("ih{key}" or "ih{key}_{tvg-id}").
    /// </summary>
    /// <param name="externalId">External id.</param>
    /// <returns>The key, or null if the channel did not come from this plugin.</returns>
    public static string? KeyFromExternalId(string? externalId)
    {
        if (string.IsNullOrEmpty(externalId) || !externalId.StartsWith(IptvManager.LiveTvChannelIdPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var rest = externalId[IptvManager.LiveTvChannelIdPrefix.Length..];
        var underscore = rest.IndexOf('_', StringComparison.Ordinal);
        return underscore >= 0 ? rest[..underscore] : rest;
    }

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
