using Jellyfin.Plugin.IptvHub.Channels;
using Jellyfin.Plugin.IptvHub.Configuration;
using Jellyfin.Plugin.IptvHub.Services;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Model.LiveTv;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.IptvHub.Tests;

/// <summary>
/// End-to-end refresh against real files. Set IPTVHUB_TESTDATA to a folder containing local/local.m3u and
/// local/guide.xml, with an HTTP server on 127.0.0.1:8099 serving remote.m3u and guide.xml.gz.
/// Skipped (passes trivially) when the variable is not set.
/// </summary>
public class PipelineTests
{
    [Fact]
    public async Task FullRefresh()
    {
        var data = Environment.GetEnvironmentVariable("IPTVHUB_TESTDATA");
        if (string.IsNullOrEmpty(data))
        {
            return;
        }

        var root = Path.Combine(Path.GetTempPath(), "iptvhub-test-" + Guid.NewGuid().ToString("N"));
        var paths = new Mock<IApplicationPaths>();
        paths.SetupGet(p => p.PluginsPath).Returns(Path.Combine(root, "plugins"));
        paths.SetupGet(p => p.PluginConfigurationsPath).Returns(Path.Combine(root, "plugincfg"));
        paths.SetupGet(p => p.CachePath).Returns(Path.Combine(root, "cache"));
        paths.SetupGet(p => p.DataPath).Returns(Path.Combine(root, "data"));
        var xml = new Mock<IXmlSerializer>();
        xml.Setup(x => x.DeserializeFromFile(It.IsAny<Type>(), It.IsAny<string>())).Returns(new PluginConfiguration());
        var plugin = new Plugin(paths.Object, xml.Object);
        Directory.CreateDirectory(plugin.DataFolderPath);

        var sports = new CustomCategory { Name = "My Sports", NamePattern = "sport", SortOrder = 0 };
        var favs = new CustomCategory { Name = "Favourites", SortOrder = 1 };
        var cfg = plugin.Configuration;
        cfg.Playlists = new[]
        {
            new PlaylistSource { Name = "Local", Kind = SourceKind.File, Location = Path.Combine(data, "local", "local.m3u") },
            new PlaylistSource { Name = "Remote", Kind = SourceKind.Url, Location = "http://127.0.0.1:8099/remote.m3u", UserAgent = "TestUA/1" }
        };
        cfg.EpgSources = new[] { new EpgSource { Name = "Local guide", Kind = SourceKind.File, Location = Path.Combine(data, "local", "guide.xml") } };
        cfg.Categories = new[] { sports, favs };

        // Live TV fakes.
        var liveTv = new LiveTvOptions { TunerHosts = Array.Empty<TunerHostInfo>(), ListingProviders = Array.Empty<ListingsProviderInfo>() };
        var config = new Mock<IConfigurationManager>();
        config.Setup(c => c.GetConfiguration("livetv")).Returns(() => liveTv);
        config.SetupGet(c => c.CommonApplicationPaths).Returns(paths.Object);
        var tuners = new Mock<ITunerHostManager>();
        tuners.Setup(t => t.SaveTunerHost(It.IsAny<TunerHostInfo>(), It.IsAny<bool>())).ReturnsAsync((TunerHostInfo i, bool _) =>
        {
            i.Id = string.IsNullOrEmpty(i.Id) ? Guid.NewGuid().ToString("N") : i.Id;
            liveTv.TunerHosts = liveTv.TunerHosts.Where(x => x.Id != i.Id).Append(i).ToArray();
            return i;
        });
        tuners.Setup(t => t.DeleteTunerHost(It.IsAny<string>()))
            .Callback((string id) => liveTv.TunerHosts = liveTv.TunerHosts.Where(x => x.Id != id).ToArray());
        var listings = new Mock<IListingsManager>();
        listings.Setup(l => l.SaveListingProvider(It.IsAny<ListingsProviderInfo>(), false, false)).ReturnsAsync((ListingsProviderInfo i, bool _, bool _) =>
        {
            i.Id = "lp1";
            liveTv.ListingProviders = liveTv.ListingProviders.Append(i).ToArray();
            return i;
        });
        var tasks = new Mock<ITaskManager>();
        tasks.SetupGet(t => t.ScheduledTasks).Returns(Array.Empty<IScheduledTaskWorker>());
        var http = new Mock<IHttpClientFactory>();
        http.Setup(h => h.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient());

        var publisher = new LiveTvPublisher(config.Object, tuners.Object, listings.Object, tasks.Object, NullLogger<LiveTvPublisher>.Instance);
        var manager = new IptvManager(new SourceFetcher(http.Object, NullLogger<SourceFetcher>.Instance), publisher, NullLogger<IptvManager>.Instance);

        var snap = await manager.RefreshAsync(true, CancellationToken.None);
        Assert.Empty(snap.Errors);
        Assert.Equal(5, snap.Channels.Count);

        // Guide matching: by tvg-id, by name, and via the remote playlist's gzipped x-tvg-url guide.
        var byName = snap.Channels.ToDictionary(c => c.OriginalName);
        Assert.Equal("news1", byName["News One HD"].EpgId);
        Assert.Equal(string.Empty, byName["Sport One"].EpgId);
        Assert.Equal("sport.two", byName["Sport Two"].EpgId);
        Assert.Equal("kids.tv", byName["Kids TV"].EpgId);
        Assert.Equal("movies", byName["Movie Channel"].EpgId);
        Assert.Equal("TestUA/1", byName["Kids TV"].Headers["User-Agent"]);
        Assert.NotNull(snap.GetNowNext(byName["News One HD"], DateTime.UtcNow).Now);

        // Categories: rule-based custom category plus the playlists' own groups.
        Assert.Equal(2, snap.CustomCategories.Single(c => c.Id == sports.Id).ChannelCount);
        Assert.Equal(new[] { "Kids", "News", "Sports", "Uncategorized" }, snap.PlaylistGroups.Select(g => g.Name));

        // Each playlist group became a real category (playlist order, Uncategorized last) with its channels in it.
        Assert.Equal(new[] { "My Sports", "Favourites", "News", "Sports", "Kids", "Uncategorized" }, cfg.Categories.OrderBy(c => c.SortOrder).Select(c => c.Name));
        var sportsGroup = cfg.Categories.Single(c => c.Name == "Sports");
        Assert.Equal(2, snap.CustomCategories.Single(c => c.Id == sportsGroup.Id).ChannelCount);
        Assert.Contains(sportsGroup.Id, byName["Sport Two"].CategoryIds);

        // Explicit assignment and overrides, rebuilt from cache (no forced download).
        favs.ChannelKeys = new[] { byName["Kids TV"].Key, byName["News One HD"].Key };
        cfg.Overrides = new[]
        {
            new ChannelOverride { Key = byName["Sport One"].Key, Hidden = true },
            new ChannelOverride { Key = byName["Movie Channel"].Key, Name = "Movies!", Number = "7" }
        };
        snap = await manager.RefreshAsync(false, CancellationToken.None);
        Assert.Equal(2, snap.CustomCategories.Single(c => c.Id == favs.Id).ChannelCount);
        Assert.Equal(1, snap.CustomCategories.Single(c => c.Id == sports.Id).ChannelCount);
        Assert.Equal("Movies!", snap.Channels[0].Name);

        // Live TV: one tuner per playlist and one XMLTV provider, backed by generated files.
        Assert.Equal(2, liveTv.TunerHosts.Length);
        Assert.All(liveTv.TunerHosts, t => Assert.True(File.Exists(t.Url)));
        Assert.Single(liveTv.ListingProviders);
        var localM3u = File.ReadAllText(liveTv.TunerHosts.Single(t => t.FriendlyName.EndsWith("Local", StringComparison.Ordinal)).Url);
        Assert.DoesNotContain("Sport One", localM3u);
        Assert.Contains("group-title=\"Favourites\"", localM3u);
        var guide = File.ReadAllText(IptvManager.GuidePath);
        Assert.Contains("channel=\"kids.tv\"", guide);
        Assert.Contains("channel=\"sport.two\"", guide);

        // Channel browsing: custom categories, playlist categories, All Channels.
        var channel = new IptvHubChannel(manager);
        var top = await channel.GetChannelItems(new InternalChannelItemQuery(), CancellationToken.None);
        Assert.Equal(new[] { "My Sports", "Favourites", "News", "Sports", "Kids", "Uncategorized", "All Channels" }, top.Items.Select(i => i.Name));
        var favItems = await channel.GetChannelItems(new InternalChannelItemQuery { FolderId = top.Items[1].Id }, CancellationToken.None);
        Assert.Equal(2, favItems.Items.Count);
        Assert.Contains("Now: Bulletin", favItems.Items.Single(i => i.Name.Contains("News", StringComparison.Ordinal)).Overview, StringComparison.Ordinal);
        var media = await channel.GetChannelItemMediaInfo(favItems.Items[0].Id, CancellationToken.None);
        Assert.True(media.Single().IsInfiniteStream);

        // Hiding a category removes its folder; "hide channels" hides everything in it.
        cfg.Categories.Single(c => c.Name == "Uncategorized").Hidden = true;
        cfg.Categories.Single(c => c.Name == "Kids").HideChannels = true;
        snap = await manager.RefreshAsync(false, CancellationToken.None);
        Assert.True(snap.ByKey[byName["Kids TV"].Key].Hidden);
        top = await channel.GetChannelItems(new InternalChannelItemQuery(), CancellationToken.None);
        Assert.Equal(new[] { "My Sports", "Favourites", "News", "Sports", "All Channels" }, top.Items.Select(i => i.Name));

        // Turning Live TV registration off removes what the plugin added.
        cfg.RegisterWithLiveTv = false;
        await manager.RefreshAsync(false, CancellationToken.None);
        Assert.Empty(liveTv.TunerHosts);
        listings.Verify(l => l.DeleteListingsProvider("lp1"), Times.Once);

        Directory.Delete(root, true);
    }
}
