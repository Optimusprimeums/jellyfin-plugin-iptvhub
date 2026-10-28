using System.IO.Compression;
using System.Text;
using Jellyfin.Plugin.IptvHub.Parsing;
using Xunit;

namespace Jellyfin.Plugin.IptvHub.Tests;

public class ParserTests
{
    [Fact]
    public void ParsesExtendedM3u()
    {
        const string m3u = """
            #EXTM3U x-tvg-url="http://example.com/a.xml.gz,http://example.com/b.xml"
            #EXTINF:-1 tvg-id="bbc1.uk" tvg-name="BBC One" tvg-logo="http://x/bbc.png" group-title="UK, News" tvg-chno="101",BBC One HD
            http://stream/bbc1.m3u8
            #EXTINF:-1,Plain Channel
            #EXTGRP:Misc
            #EXTVLCOPT:http-user-agent=VLC/3.0
            http://stream/plain.ts
            http://stream/bare.ts|Referer=http%3A%2F%2Fsite&User-Agent=Foo
            """;
        var guides = new List<string>();
        var entries = M3uParser.Parse(new StringReader(m3u), guides);

        Assert.Equal(new[] { "http://example.com/a.xml.gz", "http://example.com/b.xml" }, guides);
        Assert.Equal(3, entries.Count);

        var bbc = entries[0];
        Assert.Equal("BBC One HD", bbc.Name);
        Assert.Equal("bbc1.uk", bbc.TvgId);
        Assert.Equal("UK, News", bbc.Group);
        Assert.Equal("101", bbc.Number);
        Assert.Equal("http://x/bbc.png", bbc.Logo);
        Assert.Equal("http://stream/bbc1.m3u8", bbc.Url);

        Assert.Equal("Plain Channel", entries[1].Name);
        Assert.Equal("Misc", entries[1].Group);
        Assert.Equal("VLC/3.0", entries[1].Headers["User-Agent"]);

        Assert.Equal("http://stream/bare.ts", entries[2].Url);
        Assert.Equal("http://site", entries[2].Headers["Referer"]);
        Assert.Equal("Foo", entries[2].Headers["User-Agent"]);
    }

    [Fact]
    public void PlaylistGroupsBecomeCategories()
    {
        var cfg = new Configuration.PluginConfiguration
        {
            Categories = new[] { new Configuration.CustomCategory { Name = "kids", SortOrder = 5 } }
        };

        Assert.True(Services.IptvManager.SyncGroupCategories(cfg, new[] { "Uncategorized", "News", "Kids", "News", "Sports" }));
        Assert.Equal(new[] { "kids", "News", "Sports", "Uncategorized" }, cfg.Categories.OrderBy(c => c.SortOrder).Select(c => c.Name));
        Assert.Equal(new[] { "Kids" }, cfg.Categories.Single(c => c.Name == "kids").Groups);
        Assert.Equal(new[] { "News" }, cfg.Categories.Single(c => c.Name == "News").Groups);

        // A deleted category is not re-created; nothing new means nothing to save.
        cfg.Categories = cfg.Categories.Where(c => c.Name != "Sports").ToArray();
        Assert.False(Services.IptvManager.SyncGroupCategories(cfg, new[] { "News", "Sports" }));
        Assert.DoesNotContain(cfg.Categories, c => c.Name == "Sports");

        // A brand-new group still shows up later.
        Assert.True(Services.IptvManager.SyncGroupCategories(cfg, new[] { "Movies" }));
        Assert.Contains(cfg.Categories, c => c.Name == "Movies");

        // Turned off: nothing happens.
        cfg.AutoCreateGroupCategories = false;
        Assert.False(Services.IptvManager.SyncGroupCategories(cfg, new[] { "Docs" }));
    }

    [Theory]
    [InlineData("20240131203000 +0100", "2024-01-31T19:30:00")]
    [InlineData("20240131203000 -05:00", "2024-02-01T01:30:00")]
    [InlineData("202401312030", "2024-01-31T20:30:00")]
    [InlineData("20240131203000", "2024-01-31T20:30:00")]
    public void ParsesXmltvTimes(string raw, string expectedUtc)
    {
        Assert.True(XmltvReader.TryParseTime(raw, out var utc));
        Assert.Equal(DateTime.Parse(expectedUtc, System.Globalization.CultureInfo.InvariantCulture), utc);
        Assert.Equal(DateTimeKind.Utc, utc.Kind);
    }

    [Fact]
    public void ReadsGzippedXmltv()
    {
        var now = DateTime.UtcNow;
        string T(DateTime d) => XmltvReader.FormatTime(d);
        var xml = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE tv SYSTEM "xmltv.dtd">
            <tv>
              <channel id="bbc1.uk"><display-name>BBC One</display-name><icon src="http://x/i.png"/></channel>
              <channel id="other"><display-name>Other</display-name></channel>
              <programme start="{T(now.AddHours(-1))}" stop="{T(now.AddHours(1))}" channel="BBC1.UK">
                <title>News</title><desc>Headlines</desc><category>News</category>
              </programme>
              <programme start="{T(now.AddHours(1))}" stop="{T(now.AddHours(2))}" channel="bbc1.uk"><title>Film</title></programme>
              <programme start="{T(now.AddDays(-3))}" stop="{T(now.AddDays(-3).AddHours(1))}" channel="bbc1.uk"><title>Old</title></programme>
              <programme start="{T(now)}" stop="{T(now.AddHours(1))}" channel="other"><title>Skip</title></programme>
            </tv>
            """;
        var path = Path.GetTempFileName();
        try
        {
            using (var fs = File.Create(path))
            using (var gz = new GZipStream(fs, CompressionLevel.Fastest))
            {
                gz.Write(Encoding.UTF8.GetBytes(xml));
            }

            var channels = XmltvReader.ReadChannels(path, CancellationToken.None);
            Assert.Equal(2, channels.Count);
            Assert.Equal("BBC One", channels[0].DisplayNames.Single());
            Assert.Equal("http://x/i.png", channels[0].Icon);

            var titles = new List<string>();
            XmltvReader.ReadProgrammes(
                path,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "bbc1.uk" },
                now.AddHours(-6),
                now.AddDays(1),
                (p, _) => titles.Add(p.Title),
                CancellationToken.None);
            Assert.Equal(new[] { "News", "Film" }, titles);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
