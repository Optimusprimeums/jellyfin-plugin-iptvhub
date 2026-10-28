using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.IptvHub.Models;
using Jellyfin.Plugin.IptvHub.Services;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Channels;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.MediaInfo;

namespace Jellyfin.Plugin.IptvHub.Channels;

/// <summary>
/// Browsable "IPTV Hub" channel: custom categories, playlist categories and All Channels as folders.
/// </summary>
public class IptvHubChannel : IChannel, IRequiresMediaInfoCallback, IHasCacheKey
{
    private const string AllFolder = "all";
    private const string CustomPrefix = "cat_";
    private const string GroupPrefix = "grp_";
    private const char Separator = '~';

    private readonly IptvManager _manager;

    public IptvHubChannel(IptvManager manager)
    {
        _manager = manager;
    }

    /// <inheritdoc />
    public string Name => "IPTV Hub";

    /// <inheritdoc />
    public string Description => "Live IPTV channels grouped by category.";

    /// <inheritdoc />
    public string DataVersion => _manager.Snapshot.Version;

    /// <inheritdoc />
    public string HomePageUrl => string.Empty;

    /// <inheritdoc />
    public ChannelParentalRating ParentalRating => ChannelParentalRating.GeneralAudience;

    /// <inheritdoc />
    public string GetCacheKey(string? userId)
    {
        // Change every 10 minutes so the "now / next" text stays current.
        var bucket = DateTime.UtcNow.Ticks / TimeSpan.FromMinutes(10).Ticks;
        return _manager.Snapshot.Version + "-" + bucket.ToString(CultureInfo.InvariantCulture);
    }

    /// <inheritdoc />
    public InternalChannelFeatures GetChannelFeatures() => new()
    {
        MediaTypes = new List<ChannelMediaType> { ChannelMediaType.Video },
        ContentTypes = new List<ChannelMediaContentType> { ChannelMediaContentType.Clip },
        SupportsSortOrderToggle = false,
        DefaultSortFields = new List<ChannelItemSortField> { ChannelItemSortField.Name },
        AutoRefreshLevels = 2
    };

    /// <inheritdoc />
    public bool IsEnabledFor(string userId) => true;

    /// <inheritdoc />
    public Task<ChannelItemResult> GetChannelItems(InternalChannelItemQuery query, CancellationToken cancellationToken)
    {
        var snapshot = _manager.Snapshot;
        var cfg = Plugin.Instance!.Configuration;
        var folderId = query.FolderId ?? string.Empty;
        List<ChannelItemInfo> items;

        if (folderId.Length == 0)
        {
            items = new List<ChannelItemInfo>();
            items.AddRange(snapshot.CustomCategories.Where(c => !c.Hidden && c.ChannelCount > 0).Select(c => Folder(CustomPrefix + c.Id, c.Name, c.ChannelCount)));

            // With automatic categories the playlist groups are already categories; don't list them twice.
            if (cfg.ShowPlaylistGroups && !cfg.AutoCreateGroupCategories)
            {
                items.AddRange(snapshot.PlaylistGroups.Select(g => Folder(GroupPrefix + g.Id, g.Name, g.ChannelCount)));
            }

            if (cfg.ShowAllChannelsFolder || items.Count == 0)
            {
                items.Add(Folder(AllFolder, "All Channels", snapshot.Channels.Count(c => !c.Hidden)));
            }
        }
        else
        {
            IEnumerable<IptvChannel> channels = snapshot.Channels.Where(c => !c.Hidden);
            if (folderId.StartsWith(CustomPrefix, StringComparison.Ordinal))
            {
                var id = folderId[CustomPrefix.Length..];
                channels = channels.Where(c => c.CategoryIds.Contains(id));
            }
            else if (folderId.StartsWith(GroupPrefix, StringComparison.Ordinal))
            {
                var id = folderId[GroupPrefix.Length..];
                channels = channels.Where(c => IptvManager.GroupId(c.Group) == id);
            }

            var now = DateTime.UtcNow;
            items = channels.Select(c => Item(folderId, c, snapshot, now)).ToList();
        }

        var total = items.Count;
        IEnumerable<ChannelItemInfo> page = items;
        if (query.StartIndex is > 0)
        {
            page = page.Skip(query.StartIndex.Value);
        }

        if (query.Limit is > 0)
        {
            page = page.Take(query.Limit.Value);
        }

        return Task.FromResult(new ChannelItemResult { Items = page.ToList(), TotalRecordCount = total });
    }

    /// <inheritdoc />
    public Task<IEnumerable<MediaSourceInfo>> GetChannelItemMediaInfo(string id, CancellationToken cancellationToken)
    {
        var key = id[(id.LastIndexOf(Separator) + 1)..];
        IEnumerable<MediaSourceInfo> result = _manager.Snapshot.ByKey.TryGetValue(key, out var channel)
            ? new[] { MediaSource(channel) }
            : Array.Empty<MediaSourceInfo>();
        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task<DynamicImageResponse> GetChannelImage(ImageType type, CancellationToken cancellationToken)
        => Task.FromResult(new DynamicImageResponse { HasImage = false });

    /// <inheritdoc />
    public IEnumerable<ImageType> GetSupportedChannelImages() => Array.Empty<ImageType>();

    /// <summary>
    /// Builds a playable media source for a channel.
    /// </summary>
    /// <param name="channel">Channel.</param>
    /// <returns>Media source.</returns>
    public static MediaSourceInfo MediaSource(IptvChannel channel)
    {
        var path = channel.Url.Split('?')[0].ToLowerInvariant();
        string? container = path.EndsWith(".m3u8", StringComparison.Ordinal) ? "hls"
            : path.EndsWith(".ts", StringComparison.Ordinal) ? "ts"
            : path.EndsWith(".mp4", StringComparison.Ordinal) ? "mp4"
            : path.EndsWith(".mkv", StringComparison.Ordinal) ? "mkv"
            : null;

        return new MediaSourceInfo
        {
            Id = channel.Key,
            Name = channel.Name,
            Path = channel.Url,
            Protocol = MediaProtocol.Http,
            IsRemote = true,
            IsInfiniteStream = true,
            Container = container,
            SupportsDirectPlay = false,
            SupportsDirectStream = true,
            SupportsTranscoding = true,
            SupportsProbing = true,
            RequiresOpening = false,
            RequiresClosing = false,
            IgnoreDts = true,
            AnalyzeDurationMs = 3000,
            RequiredHttpHeaders = new Dictionary<string, string>(channel.Headers, StringComparer.OrdinalIgnoreCase)
        };
    }

    private static ChannelItemInfo Folder(string id, string name, int count) => new()
    {
        Id = id,
        Name = name,
        Type = ChannelItemType.Folder,
        FolderType = ChannelFolderType.Container,
        Overview = count.ToString(CultureInfo.InvariantCulture) + " channels"
    };

    private static ChannelItemInfo Item(string folderId, IptvChannel channel, IptvSnapshot snapshot, DateTime now)
    {
        var (current, next) = snapshot.GetNowNext(channel, now);
        var overview = new StringBuilder();
        if (current is not null)
        {
            overview.Append("Now: ").Append(current.Title)
                .Append(" (until ").Append(current.EndUtc.ToLocalTime().ToString("t", CultureInfo.CurrentCulture)).Append(')');
            if (current.Description.Length > 0)
            {
                overview.Append("\n").Append(current.Description);
            }
        }

        if (next is not null)
        {
            overview.Append(overview.Length > 0 ? "\n\n" : string.Empty)
                .Append("Next: ").Append(next.Title)
                .Append(" at ").Append(next.StartUtc.ToLocalTime().ToString("t", CultureInfo.CurrentCulture));
        }

        var name = channel.Number.Length > 0 ? channel.Number + "  " + channel.Name : channel.Name;
        return new ChannelItemInfo
        {
            Id = folderId + Separator + channel.Key,
            Name = name,
            Type = ChannelItemType.Media,
            MediaType = ChannelMediaType.Video,
            ContentType = ChannelMediaContentType.Clip,
            IsLiveStream = true,
            ImageUrl = channel.Logo,
            Overview = overview.ToString(),
            Genres = current?.Categories ?? new List<string>(),
            Tags = new List<string> { channel.Group },
            DateCreated = snapshot.BuiltUtc,
            MediaSources = new List<MediaSourceInfo> { MediaSource(channel) }
        };
    }
}
