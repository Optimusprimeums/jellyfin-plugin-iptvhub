using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.IptvHub.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Model.LiveTv;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.IptvHub.Services;

/// <summary>
/// Registers the generated playlists as M3U tuners and the generated guide as an XMLTV provider,
/// so channels show up in Jellyfin's Live TV with a full guide, recordings and stream sharing.
/// </summary>
public class LiveTvPublisher
{
    private const string FriendlyPrefix = "IPTV Hub - ";

    private readonly IConfigurationManager _config;
    private readonly ITunerHostManager _tunerHostManager;
    private readonly IListingsManager _listingsManager;
    private readonly ITaskManager _taskManager;
    private readonly ILogger<LiveTvPublisher> _logger;

    public LiveTvPublisher(
        IConfigurationManager config,
        ITunerHostManager tunerHostManager,
        IListingsManager listingsManager,
        ITaskManager taskManager,
        ILogger<LiveTvPublisher> logger)
    {
        _config = config;
        _tunerHostManager = tunerHostManager;
        _listingsManager = listingsManager;
        _taskManager = taskManager;
        _logger = logger;
    }

    /// <summary>
    /// Brings Jellyfin's Live TV configuration in line with the plugin's sources.
    /// </summary>
    /// <param name="cfg">Plugin configuration.</param>
    /// <param name="sources">Playlists that loaded successfully (their files have been written).</param>
    /// <param name="hasGuide">Whether a guide was produced.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task PublishAsync(PluginConfiguration cfg, IReadOnlyList<PlaylistSource> sources, bool hasGuide, CancellationToken cancellationToken)
    {
        var outputDir = IptvManager.OutputDirectory;
        var options = (LiveTvOptions)_config.GetConfiguration("livetv");
        var ours = options.TunerHosts.Where(t => IsOurs(t.Url, outputDir)).ToList();
        var changed = false;

        // Configured sources that failed to load this time keep their existing tuner (and last good file).
        var keep = new HashSet<string>(
            cfg.Playlists.Where(p => p.Enabled).Select(p => IptvManager.PlaylistPath(p.Id)),
            StringComparer.OrdinalIgnoreCase);

        foreach (var tuner in ours.Where(t => !cfg.RegisterWithLiveTv || !keep.Contains(t.Url)))
        {
            _logger.LogInformation("IPTV Hub: removing Live TV tuner {Name}", tuner.FriendlyName);
            _tunerHostManager.DeleteTunerHost(tuner.Id);
            changed = true;
        }

        var guideProvider = options.ListingProviders.FirstOrDefault(p =>
            string.Equals(p.Type, "xmltv", StringComparison.OrdinalIgnoreCase)
            && string.Equals(p.Path, IptvManager.GuidePath, StringComparison.OrdinalIgnoreCase));

        if (!cfg.RegisterWithLiveTv)
        {
            if (guideProvider is not null)
            {
                _listingsManager.DeleteListingsProvider(guideProvider.Id);
            }

            return;
        }

        foreach (var source in sources)
        {
            var path = IptvManager.PlaylistPath(source.Id);
            var friendly = FriendlyPrefix + (source.Name.Length > 0 ? source.Name : source.Id);
            var existing = ours.FirstOrDefault(t => string.Equals(t.Url, path, StringComparison.OrdinalIgnoreCase));
            if (existing is not null
                && existing.FriendlyName == friendly
                && (existing.UserAgent ?? string.Empty) == source.UserAgent)
            {
                continue;
            }

            _logger.LogInformation("IPTV Hub: registering Live TV tuner {Name}", friendly);
            await _tunerHostManager.SaveTunerHost(
                new TunerHostInfo
                {
                    Id = existing?.Id ?? string.Empty,
                    Type = "m3u",
                    Url = path,
                    FriendlyName = friendly,
                    UserAgent = source.UserAgent,
                    TunerCount = existing?.TunerCount ?? 0,
                    AllowHWTranscoding = existing?.AllowHWTranscoding ?? true,
                    AllowStreamSharing = existing?.AllowStreamSharing ?? true,
                    AllowFmp4TranscodingContainer = existing?.AllowFmp4TranscodingContainer ?? false,
                    EnableStreamLooping = existing?.EnableStreamLooping ?? false,
                    IgnoreDts = existing?.IgnoreDts ?? true,
                    FallbackMaxStreamingBitrate = existing?.FallbackMaxStreamingBitrate ?? 30000000
                },
                true).ConfigureAwait(false);
            changed = true;
        }

        if (hasGuide && guideProvider is null)
        {
            _logger.LogInformation("IPTV Hub: registering XMLTV guide {Path}", IptvManager.GuidePath);
            guideProvider = await _listingsManager.SaveListingProvider(
                new ListingsProviderInfo { Type = "xmltv", Path = IptvManager.GuidePath, EnableAllTuners = true },
                false,
                false).ConfigureAwait(false);
            changed = true;
        }

        if (guideProvider is not null)
        {
            // Jellyfin caches XMLTV files for an hour; drop its copy so the new guide is read on the next refresh.
            var cached = Path.Combine(_config.CommonApplicationPaths.CachePath, "xmltv", guideProvider.Id + ".xml");
            if (File.Exists(cached))
            {
                File.Delete(cached);
            }
        }

        // Only kick off a guide refresh when the published data actually differs from last time.
        var fingerprint = Fingerprint(sources.Select(s => IptvManager.PlaylistPath(s.Id)).Append(IptvManager.GuidePath));
        var fingerprintFile = Path.Combine(outputDir, ".published");
        var previous = File.Exists(fingerprintFile) ? await File.ReadAllTextAsync(fingerprintFile, cancellationToken).ConfigureAwait(false) : string.Empty;
        if (changed || previous != fingerprint)
        {
            await File.WriteAllTextAsync(fingerprintFile, fingerprint, cancellationToken).ConfigureAwait(false);
            var guideTask = _taskManager.ScheduledTasks.FirstOrDefault(t => t.ScheduledTask.Key == "RefreshGuide");
            if (guideTask is not null)
            {
                _logger.LogInformation("IPTV Hub: queueing Live TV guide refresh");
                _taskManager.QueueScheduledTask(guideTask.ScheduledTask, new TaskOptions());
            }
        }
    }

    private static bool IsOurs(string? url, string outputDir)
        => !string.IsNullOrEmpty(url) && url.StartsWith(outputDir, StringComparison.OrdinalIgnoreCase);

    private static string Fingerprint(IEnumerable<string> files)
    {
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var file in files.Where(File.Exists))
        {
            sha.AppendData(File.ReadAllBytes(file));
        }

        return Convert.ToHexString(sha.GetHashAndReset());
    }
}
