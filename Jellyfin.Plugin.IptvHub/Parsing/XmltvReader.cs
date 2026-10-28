using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using Jellyfin.Plugin.IptvHub.Models;

namespace Jellyfin.Plugin.IptvHub.Parsing;

/// <summary>
/// Streams XMLTV files so very large guides don't have to fit in memory.
/// </summary>
public static class XmltvReader
{
    /// <summary>
    /// Opens a file, transparently decompressing gzip.
    /// </summary>
    /// <param name="path">File path.</param>
    /// <returns>A readable stream.</returns>
    public static Stream OpenMaybeGzip(string path)
    {
        var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.SequentialScan);
        var b1 = fs.ReadByte();
        var b2 = fs.ReadByte();
        fs.Position = 0;
        return b1 == 0x1f && b2 == 0x8b ? new GZipStream(fs, CompressionMode.Decompress) : fs;
    }

    /// <summary>
    /// Reads all &lt;channel&gt; elements.
    /// </summary>
    /// <param name="path">XMLTV file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Channels.</returns>
    public static List<EpgChannel> ReadChannels(string path, CancellationToken cancellationToken)
    {
        var list = new List<EpgChannel>();
        using var stream = OpenMaybeGzip(path);
        using var reader = XmlReader.Create(stream, Settings());
        reader.MoveToContent();
        while (!reader.EOF)
        {
            if (reader.NodeType == XmlNodeType.Element && reader.Name == "channel")
            {
                cancellationToken.ThrowIfCancellationRequested();
                var el = (XElement)XNode.ReadFrom(reader);
                var id = (string?)el.Attribute("id");
                if (!string.IsNullOrWhiteSpace(id))
                {
                    list.Add(new EpgChannel
                    {
                        Id = id.Trim(),
                        DisplayNames = el.Elements("display-name").Select(e => e.Value.Trim()).Where(v => v.Length > 0).ToList(),
                        Icon = (string?)el.Element("icon")?.Attribute("src") ?? string.Empty
                    });
                }
            }
            else if (reader.NodeType == XmlNodeType.Element && reader.Name == "programme")
            {
                // Channels are listed before programmes in practically every guide; skip the rest quickly.
                reader.Skip();
            }
            else
            {
                reader.Read();
            }
        }

        return list;
    }

    /// <summary>
    /// Reads programmes for the wanted channels that overlap the time window.
    /// </summary>
    /// <param name="path">XMLTV file.</param>
    /// <param name="wantedIds">Channel ids to keep (case-insensitive set).</param>
    /// <param name="fromUtc">Window start.</param>
    /// <param name="toUtc">Window end.</param>
    /// <param name="onProgramme">Called with the parsed model and the raw element.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static void ReadProgrammes(
        string path,
        ISet<string> wantedIds,
        DateTime fromUtc,
        DateTime toUtc,
        Action<EpgProgramme, XElement> onProgramme,
        CancellationToken cancellationToken)
    {
        using var stream = OpenMaybeGzip(path);
        using var reader = XmlReader.Create(stream, Settings());
        reader.MoveToContent();
        var count = 0;
        while (!reader.EOF)
        {
            if (reader.NodeType == XmlNodeType.Element && reader.Name == "programme")
            {
                var channel = reader.GetAttribute("channel")?.Trim();
                if (channel is null || !wantedIds.Contains(channel))
                {
                    reader.Skip();
                    continue;
                }

                if (!TryParseTime(reader.GetAttribute("start"), out var start))
                {
                    reader.Skip();
                    continue;
                }

                var hasStop = TryParseTime(reader.GetAttribute("stop"), out var stop);
                if (!hasStop)
                {
                    stop = start.AddHours(1);
                }

                if (stop < fromUtc || start > toUtc)
                {
                    reader.Skip();
                    continue;
                }

                if ((++count & 1023) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                var el = (XElement)XNode.ReadFrom(reader);
                var episode = el.Elements("episode-num").FirstOrDefault(e => (string?)e.Attribute("system") == "onscreen")
                    ?? el.Elements("episode-num").FirstOrDefault();
                var programme = new EpgProgramme
                {
                    ChannelId = channel,
                    StartUtc = start,
                    EndUtc = stop,
                    Title = el.Element("title")?.Value.Trim() ?? string.Empty,
                    SubTitle = el.Element("sub-title")?.Value.Trim() ?? string.Empty,
                    Description = el.Element("desc")?.Value.Trim() ?? string.Empty,
                    Categories = el.Elements("category").Select(e => e.Value.Trim()).Where(v => v.Length > 0).Distinct().ToList(),
                    Icon = (string?)el.Element("icon")?.Attribute("src") ?? string.Empty,
                    EpisodeNum = episode?.Value.Trim() ?? string.Empty
                };

                if (!hasStop)
                {
                    el.SetAttributeValue("stop", FormatTime(stop));
                }

                onProgramme(programme, el);
            }
            else
            {
                reader.Read();
            }
        }
    }

    /// <summary>
    /// Parses an XMLTV timestamp such as "20240131203000 +0100".
    /// </summary>
    /// <param name="value">Raw value.</param>
    /// <param name="utc">Parsed UTC time.</param>
    /// <returns>Whether parsing succeeded.</returns>
    public static bool TryParseTime(string? value, out DateTime utc)
    {
        utc = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        value = value.Trim();
        var digits = 0;
        while (digits < value.Length && char.IsDigit(value[digits]))
        {
            digits++;
        }

        if (digits < 12)
        {
            return false;
        }

        var stamp = value[..Math.Min(digits, 14)].PadRight(14, '0');
        if (!DateTime.TryParseExact(stamp, "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
        {
            return false;
        }

        var offset = TimeSpan.Zero;
        var rest = value[digits..].Trim();
        if (rest.Length >= 5 && (rest[0] == '+' || rest[0] == '-'))
        {
            var hhmm = rest[1..].Replace(":", string.Empty, StringComparison.Ordinal);
            if (hhmm.Length >= 4
                && int.TryParse(hhmm.AsSpan(0, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var h)
                && int.TryParse(hhmm.AsSpan(2, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var m))
            {
                offset = new TimeSpan(h, m, 0);
                if (rest[0] == '-')
                {
                    offset = -offset;
                }
            }
        }

        utc = DateTime.SpecifyKind(local - offset, DateTimeKind.Utc);
        return true;
    }

    /// <summary>
    /// Formats a UTC time as an XMLTV timestamp.
    /// </summary>
    /// <param name="utc">UTC time.</param>
    /// <returns>Timestamp text.</returns>
    public static string FormatTime(DateTime utc) => utc.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) + " +0000";

    private static XmlReaderSettings Settings() => new()
    {
        DtdProcessing = DtdProcessing.Ignore,
        XmlResolver = null,
        IgnoreComments = true,
        IgnoreWhitespace = true,
        CheckCharacters = false
    };
}
