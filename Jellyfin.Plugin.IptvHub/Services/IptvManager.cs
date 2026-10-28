using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Jellyfin.Plugin.IptvHub.Configuration;
using Jellyfin.Plugin.IptvHub.Models;
using Jellyfin.Plugin.IptvHub.Parsing;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.IptvHub.Services;

/// <summary>
/// Immutable result of a refresh.
/// </summary>
public sealed class IptvSnapshot
{
    public static readonly IptvSnapshot Empty = new();

    public IReadOnlyList<IptvChannel> Channels { get; init; } = Array.Empty<IptvChannel>();

    public IReadOnlyDictionary<string, IptvChannel> ByKey { get; init; } = new Dictionary<string, IptvChannel>();

    public IReadOnlyList<CategoryView> CustomCategories { get; init; } = Array.Empty<CategoryView>();

    public IReadOnlyList<CategoryView> PlaylistGroups { get; init; } = Array.Empty<CategoryView>();

    /// <summary>Gets programmes by EPG channel id (case-insensitive), sorted by start.</summary>
    public IReadOnlyDictionary<string, List<EpgProgramme>> Programmes { get; init; } = new Dictionary<string, List<EpgProgramme>>();

    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    public int EpgChannelCount { get; init; }

    public int ProgrammeCount { get; init; }

    public DateTime BuiltUtc { get; init; }

    public string Version { get; init; } = "0";

    /// <summary>Gets the guide currently airing and the one after it.</summary>
    /// <param name="channel">Channel.</param>
    /// <param name="nowUtc">Current time.</param>
    /// <returns>Now and next programmes (either may be null).</returns>
    public (EpgProgramme? Now, EpgProgramme? Next) GetNowNext(IptvChannel channel, DateTime nowUtc)
    {
        if (channel.EpgId.Length == 0 || !Programmes.TryGetValue(channel.EpgId, out var list))
        {
            return (null, null);
        }

        EpgProgramme? now = null;
        foreach (var p in list)
        {
            if (p.EndUtc <= nowUtc)
            {
                continue;
            }

            if (p.StartUtc <= nowUtc)
            {
                now = p;
                continue;
            }

            return (now, p);
        }

        return (now, null);
    }
}

/// <summary>
/// Loads playlists and guides, applies categories and overrides, and publishes the result to Jellyfin.
/// </summary>
public sealed partial class IptvManager : IDisposable
{
    /// <summary>Name used for playlist groups without a group-title.</summary>
    public const string UngroupedName = "Uncategorized";

    private readonly SourceFetcher _fetcher;
    private readonly LiveTvPublisher _publisher;
    private readonly ILogger<IptvManager> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private volatile IptvSnapshot _snapshot = IptvSnapshot.Empty;

    public IptvManager(SourceFetcher fetcher, LiveTvPublisher publisher, ILogger<IptvManager> logger)
    {
        _fetcher = fetcher;
        _publisher = publisher;
        _logger = logger;
    }

    /// <summary>Raised after every successful refresh.</summary>
    public event EventHandler? Refreshed;

    public IptvSnapshot Snapshot => _snapshot;

    public bool IsRefreshing => _lock.CurrentCount == 0;

    /// <summary>Directory holding generated files for Live TV.</summary>
    public static string OutputDirectory => Path.Combine(Plugin.Instance!.DataFolderPath, "livetv");

    /// <summary>Path of the generated, filtered XMLTV guide.</summary>
    public static string GuidePath => Path.Combine(OutputDirectory, "guide.xml");

    /// <summary>Path of the generated playlist for one source.</summary>
    /// <param name="sourceId">Playlist source id.</param>
    /// <returns>File path.</returns>
    public static string PlaylistPath(string sourceId) => Path.Combine(OutputDirectory, "playlist-" + sourceId + ".m3u");

    /// <summary>
    /// Reloads everything.
    /// </summary>
    /// <param name="forceDownload">Ignore cached downloads.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The new snapshot.</returns>
    public async Task<IptvSnapshot> RefreshAsync(bool forceDownload, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var snapshot = await BuildAsync(Plugin.Instance!.Configuration, forceDownload, cancellationToken).ConfigureAwait(false);
            _snapshot = snapshot;
            _logger.LogInformation(
                "IPTV Hub: {Channels} channels, {EpgChannels} guide channels, {Programmes} programmes, {Errors} errors",
                snapshot.Channels.Count,
                snapshot.EpgChannelCount,
                snapshot.ProgrammeCount,
                snapshot.Errors.Count);
            Refreshed?.Invoke(this, EventArgs.Empty);
            return snapshot;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose() => _lock.Dispose();

    private async Task<IptvSnapshot> BuildAsync(PluginConfiguration cfg, bool force, CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        var maxAge = TimeSpan.FromHours(Math.Max(1, cfg.RefreshIntervalHours));
        var overrides = cfg.Overrides.Where(o => o.Key.Length > 0)
            .GroupBy(o => o.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Last(), StringComparer.Ordinal);

        // 1. Playlists.
        var channels = new List<IptvChannel>();
        var advertisedGuides = new List<string>();
        var loadedSources = new List<PlaylistSource>();
        foreach (var source in cfg.Playlists.Where(p => p.Enabled))
        {
            try
            {
                var path = await _fetcher.GetLocalCopyAsync("pl-" + source.Id, source.Kind, source.Location, source.UserAgent, maxAge, force, cancellationToken).ConfigureAwait(false);
                List<M3uEntry> entries;
                using (var reader = new StreamReader(XmltvReader.OpenMaybeGzip(path), Encoding.UTF8, true))
                {
                    entries = M3uParser.Parse(reader, advertisedGuides);
                }

                var seen = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var e in entries)
                {
                    var baseKey = Hash(source.Id + "\n" + e.TvgId + "\n" + e.Name, 12);
                    var n = seen[baseKey] = seen.GetValueOrDefault(baseKey) + 1;
                    var channel = new IptvChannel
                    {
                        Key = n == 1 ? baseKey : baseKey + "-" + n.ToString(CultureInfo.InvariantCulture),
                        SourceId = source.Id,
                        SourceName = source.Name.Length > 0 ? source.Name : "Playlist",
                        Name = e.Name,
                        OriginalName = e.Name,
                        Url = e.Url,
                        Logo = e.Logo,
                        Number = e.Number,
                        Group = e.Group.Length > 0 ? e.Group : UngroupedName,
                        TvgId = e.TvgId,
                        Headers = new Dictionary<string, string>(e.Headers, StringComparer.OrdinalIgnoreCase)
                    };

                    if (!channel.Headers.ContainsKey("User-Agent") && !string.IsNullOrWhiteSpace(source.UserAgent))
                    {
                        channel.Headers["User-Agent"] = source.UserAgent;
                    }

                    if (overrides.TryGetValue(channel.Key, out var o))
                    {
                        channel.Hidden = o.Hidden;
                        if (o.Name.Length > 0)
                        {
                            channel.Name = o.Name;
                        }

                        if (o.Number.Length > 0)
                        {
                            channel.Number = o.Number;
                        }
                    }

                    channels.Add(channel);
                }

                loadedSources.Add(source);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "IPTV Hub: failed to load playlist {Name}", source.Name);
                errors.Add($"Playlist \"{source.Name}\": {ex.Message}");
            }
        }

        // 1b. Every new playlist group becomes a category the user can rename, merge, reorder or hide.
        if (SyncGroupCategories(cfg, channels.Select(c => c.Group)))
        {
            Plugin.Instance!.SaveConfiguration();
        }

        // 2. Guide sources: configured ones plus any advertised by the playlists.
        var guideSources = cfg.EpgSources.Where(s => s.Enabled).Select(s => (Id: s.Id, s.Name, s.Kind, s.Location)).ToList();
        if (cfg.UsePlaylistGuideUrls)
        {
            var configured = new HashSet<string>(guideSources.Select(g => g.Location.Trim()), StringComparer.OrdinalIgnoreCase);
            foreach (var url in advertisedGuides.Distinct(StringComparer.OrdinalIgnoreCase).Where(u => !configured.Contains(u)))
            {
                guideSources.Add(("auto-" + Hash(url, 12), url, SourceKind.Url, url));
            }
        }

        var guideFiles = new List<string>();
        var epgChannels = new Dictionary<string, EpgChannel>(StringComparer.OrdinalIgnoreCase);
        var byName = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var g in guideSources)
        {
            try
            {
                var path = await _fetcher.GetLocalCopyAsync("epg-" + g.Id, g.Kind, g.Location, null, maxAge, force, cancellationToken).ConfigureAwait(false);
                foreach (var ec in XmltvReader.ReadChannels(path, cancellationToken))
                {
                    if (epgChannels.TryAdd(ec.Id, ec))
                    {
                        foreach (var dn in ec.DisplayNames)
                        {
                            byName.TryAdd(Normalize(dn), ec.Id);
                        }

                        byName.TryAdd(Normalize(ec.Id), ec.Id);
                    }
                }

                guideFiles.Add(path);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "IPTV Hub: failed to load guide {Name}", g.Name);
                errors.Add($"Guide \"{g.Name}\": {ex.Message}");
            }
        }

        // 3. Match channels to guide channels: override, then tvg-id, then names.
        foreach (var c in channels)
        {
            if (overrides.TryGetValue(c.Key, out var o) && o.EpgId.Length > 0)
            {
                c.EpgId = epgChannels.TryGetValue(o.EpgId, out var forced) ? forced.Id : o.EpgId;
                continue;
            }

            if (c.TvgId.Length > 0 && epgChannels.TryGetValue(c.TvgId, out var byId))
            {
                c.EpgId = byId.Id;
                continue;
            }

            foreach (var candidate in new[] { c.TvgId, c.OriginalName, c.Name })
            {
                if (candidate.Length > 0 && byName.TryGetValue(Normalize(candidate), out var id))
                {
                    c.EpgId = id;
                    break;
                }
            }
        }

        // 4. Programmes, streamed straight into the filtered guide file.
        Directory.CreateDirectory(OutputDirectory);
        var wanted = new HashSet<string>(channels.Where(c => c.EpgId.Length > 0).Select(c => c.EpgId), StringComparer.OrdinalIgnoreCase);
        var programmes = new Dictionary<string, List<EpgProgramme>>(StringComparer.OrdinalIgnoreCase);
        var programmeCount = 0;
        var from = DateTime.UtcNow.AddHours(-6);
        var to = DateTime.UtcNow.AddDays(Math.Clamp(cfg.GuideDays, 1, 14));
        var tempGuide = GuidePath + ".tmp";
        await using (var fs = new FileStream(tempGuide, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var writer = XmlWriter.Create(fs, new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false), Async = false }))
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("tv");
            writer.WriteAttributeString("generator-info-name", "Jellyfin IPTV Hub");
            foreach (var id in wanted)
            {
                var ec = epgChannels.GetValueOrDefault(id);
                writer.WriteStartElement("channel");
                writer.WriteAttributeString("id", ec?.Id ?? id);
                var names = ec?.DisplayNames.Count > 0 ? ec.DisplayNames : new List<string> { id };
                foreach (var dn in names)
                {
                    writer.WriteElementString("display-name", dn);
                }

                if (!string.IsNullOrEmpty(ec?.Icon))
                {
                    writer.WriteStartElement("icon");
                    writer.WriteAttributeString("src", ec.Icon);
                    writer.WriteEndElement();
                }

                writer.WriteEndElement();
            }

            var dedupe = new HashSet<(string, long)>();
            foreach (var file in guideFiles)
            {
                try
                {
                    XmltvReader.ReadProgrammes(
                        file,
                        wanted,
                        from,
                        to,
                        (p, el) =>
                        {
                            var canonical = epgChannels.TryGetValue(p.ChannelId, out var ec) ? ec.Id : p.ChannelId;
                            if (!dedupe.Add((canonical.ToUpperInvariant(), p.StartUtc.Ticks)))
                            {
                                return;
                            }

                            p.ChannelId = canonical;
                            el.SetAttributeValue("channel", canonical);
                            el.WriteTo(writer);
                            if (!programmes.TryGetValue(canonical, out var list))
                            {
                                programmes[canonical] = list = new List<EpgProgramme>();
                            }

                            list.Add(p);
                            programmeCount++;
                        },
                        cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "IPTV Hub: error reading programmes from {File}", file);
                    errors.Add($"Guide file {Path.GetFileName(file)}: {ex.Message}");
                }
            }

            writer.WriteEndElement();
            writer.WriteEndDocument();
        }

        File.Move(tempGuide, GuidePath, true);
        foreach (var list in programmes.Values)
        {
            list.Sort((a, b) => a.StartUtc.CompareTo(b.StartUtc));
        }

        // 5. Categories: custom (groups + explicit + rules) and the playlists' own group-titles.
        var orderedCategories = cfg.Categories.OrderBy(c => c.SortOrder).ThenBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        foreach (var cat in orderedCategories)
        {
            var explicitKeys = new HashSet<string>(cat.ChannelKeys, StringComparer.Ordinal);
            var groups = new HashSet<string>(cat.Groups, StringComparer.OrdinalIgnoreCase);
            var nameRx = TryRegex(cat.NamePattern, cat.Name, errors);
            var groupRx = TryRegex(cat.GroupPattern, cat.Name, errors);
            foreach (var c in channels)
            {
                if (groups.Contains(c.Group)
                    || explicitKeys.Contains(c.Key)
                    || (nameRx is not null && nameRx.IsMatch(c.Name))
                    || (groupRx is not null && groupRx.IsMatch(c.Group)))
                {
                    c.CategoryIds.Add(cat.Id);
                    if (cat.HideChannels)
                    {
                        c.Hidden = true;
                    }
                }
            }
        }

        var customViews = orderedCategories.Select(cat => new CategoryView
        {
            Id = cat.Id,
            Name = cat.Name,
            Kind = "custom",
            Hidden = cat.Hidden,
            ChannelCount = channels.Count(c => !c.Hidden && c.CategoryIds.Contains(cat.Id))
        }).ToList();

        var groupViews = channels.Where(c => !c.Hidden)
            .GroupBy(c => c.Group, StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(g => string.Equals(g.Key, UngroupedName, StringComparison.Ordinal) ? 1 : 0)
            .ThenBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase)
            .Select(g => new CategoryView { Id = GroupId(g.Key), Name = g.First().Group, Kind = "playlist", ChannelCount = g.Count() })
            .ToList();

        // 6. Per-source playlists for Jellyfin's Live TV tuner.
        var categoryNames = VisibleCategoryNames(cfg);
        foreach (var source in loadedSources)
        {
            WritePlaylist(PlaylistPath(source.Id), channels.Where(c => c.SourceId == source.Id && !c.Hidden), categoryNames, false);
        }

        try
        {
            await _publisher.PublishAsync(cfg, loadedSources, programmeCount > 0 || wanted.Count > 0, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "IPTV Hub: failed to register with Live TV");
            errors.Add("Live TV registration: " + ex.Message);
        }

        var ordered = channels.OrderBy(c => SortNumber(c.Number)).ThenBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        return new IptvSnapshot
        {
            Channels = ordered,
            ByKey = ordered.GroupBy(c => c.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal),
            CustomCategories = customViews,
            PlaylistGroups = groupViews,
            Programmes = programmes,
            Errors = errors,
            EpgChannelCount = epgChannels.Count,
            ProgrammeCount = programmeCount,
            BuiltUtc = DateTime.UtcNow,
            Version = DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture)
        };
    }

    /// <summary>
    /// Writes an extended M3U for the channels.
    /// </summary>
    /// <param name="path">Destination, or null to return the text only.</param>
    /// <param name="channels">Channels to include.</param>
    /// <param name="categoryNames">Custom category id to name.</param>
    /// <param name="includeHeaders">Emit #EXTVLCOPT header lines for players that support them.</param>
    /// <returns>The playlist text.</returns>
    public static string WritePlaylist(string? path, IEnumerable<IptvChannel> channels, IReadOnlyDictionary<string, string> categoryNames, bool includeHeaders)
    {
        var sb = new StringBuilder();
        sb.Append("#EXTM3U\n");
        foreach (var c in channels)
        {
            // Jellyfin keeps a single group per channel; a custom category takes precedence over the playlist's.
            var group = c.CategoryIds.Select(id => categoryNames.GetValueOrDefault(id)).FirstOrDefault(n => !string.IsNullOrEmpty(n)) ?? c.Group;
            sb.Append("#EXTINF:-1");
            Attr(sb, "tvg-id", c.EpgId);
            Attr(sb, "tvg-name", c.Name);
            Attr(sb, "tvg-logo", c.Logo);
            Attr(sb, "tvg-chno", c.Number);
            Attr(sb, "group-title", group);
            sb.Append(',').Append(c.Name.Replace('\n', ' ')).Append('\n');
            if (includeHeaders)
            {
                if (c.Headers.TryGetValue("User-Agent", out var ua))
                {
                    sb.Append("#EXTVLCOPT:http-user-agent=").Append(ua).Append('\n');
                }

                if (c.Headers.TryGetValue("Referer", out var referer))
                {
                    sb.Append("#EXTVLCOPT:http-referrer=").Append(referer).Append('\n');
                }
            }

            sb.Append(c.Url).Append('\n');
        }

        var text = sb.ToString();
        if (path is not null)
        {
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, text, new UTF8Encoding(false));
            File.Move(tmp, path, true);
        }

        return text;
    }

    /// <summary>
    /// Creates a category for each playlist group not seen before. A group whose name matches an existing
    /// category (case-insensitive) is added to that category instead. Groups the user deleted stay deleted.
    /// </summary>
    /// <param name="cfg">Configuration to update.</param>
    /// <param name="groupsInPlaylistOrder">Group of every channel, in playlist order.</param>
    /// <returns>Whether the configuration changed and needs saving.</returns>
    public static bool SyncGroupCategories(PluginConfiguration cfg, IEnumerable<string> groupsInPlaylistOrder)
    {
        if (!cfg.AutoCreateGroupCategories)
        {
            return false;
        }

        var known = new HashSet<string>(cfg.KnownGroups, StringComparer.OrdinalIgnoreCase);
        var newGroups = groupsInPlaylistOrder.Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(g => !known.Contains(g))
            .OrderBy(g => string.Equals(g, UngroupedName, StringComparison.OrdinalIgnoreCase) ? 1 : 0)
            .ToList();
        if (newGroups.Count == 0)
        {
            return false;
        }

        var categories = cfg.Categories.ToList();
        var nextOrder = categories.Count == 0 ? 0 : categories.Max(c => c.SortOrder) + 1;
        foreach (var group in newGroups)
        {
            var existing = categories.FirstOrDefault(c =>
                string.Equals(c.Name.Trim(), group, StringComparison.OrdinalIgnoreCase)
                || c.Groups.Contains(group, StringComparer.OrdinalIgnoreCase));
            if (existing is null)
            {
                categories.Add(new CustomCategory { Name = group, Groups = new[] { group }, SortOrder = nextOrder++ });
            }
            else if (!existing.Groups.Contains(group, StringComparer.OrdinalIgnoreCase))
            {
                existing.Groups = existing.Groups.Append(group).ToArray();
            }

            known.Add(group);
        }

        cfg.Categories = categories.ToArray();
        cfg.KnownGroups = known.ToArray();
        return true;
    }

    /// <summary>Visible custom categories by id, for group-title output.</summary>
    /// <param name="cfg">Configuration.</param>
    /// <returns>Id to name.</returns>
    public static Dictionary<string, string> VisibleCategoryNames(PluginConfiguration cfg)
        => cfg.Categories.Where(c => !c.Hidden).GroupBy(c => c.Id).ToDictionary(g => g.Key, g => g.First().Name, StringComparer.Ordinal);

    /// <summary>Stable id for a playlist group-title.</summary>
    /// <param name="group">Group name.</param>
    /// <returns>Id.</returns>
    public static string GroupId(string group) => Hash(group.ToUpperInvariant(), 10);

    private static void Attr(StringBuilder sb, string name, string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            sb.Append(' ').Append(name).Append("=\"").Append(value.Replace('"', '\'')).Append('"');
        }
    }

    private static Regex? TryRegex(string pattern, string category, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return null;
        }

        try
        {
            return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250));
        }
        catch (ArgumentException ex)
        {
            errors.Add($"Category \"{category}\": invalid pattern \"{pattern}\": {ex.Message}");
            return null;
        }
    }

    private static double SortNumber(string number)
        => double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : double.MaxValue;

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlnum();

    [GeneratedRegex("(fhd|uhd|hd|sd|4k|hevc|h265)$")]
    private static partial Regex QualitySuffix();

    /// <summary>
    /// Loose channel-name key: "BBC One HD" and "bbc.one" both become "bbcone".
    /// </summary>
    private static string Normalize(string value)
    {
        var s = NonAlnum().Replace(value.ToLowerInvariant(), string.Empty);
        var stripped = QualitySuffix().Replace(s, string.Empty);
        return stripped.Length > 0 ? stripped : s;
    }

    private static string Hash(string value, int length)
        => Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(value)))[..length].ToLowerInvariant();
}
