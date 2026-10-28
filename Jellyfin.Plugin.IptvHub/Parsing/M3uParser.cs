using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.IptvHub.Models;

namespace Jellyfin.Plugin.IptvHub.Parsing;

/// <summary>
/// Parses extended M3U / M3U8 playlists.
/// </summary>
public static partial class M3uParser
{
    [GeneratedRegex("([A-Za-z0-9_\\-:]+)\\s*=\\s*\"([^\"]*)\"")]
    private static partial Regex AttributeRegex();

    /// <summary>
    /// Parses a playlist.
    /// </summary>
    /// <param name="reader">Playlist text.</param>
    /// <param name="guideUrls">Receives guide URLs advertised in the #EXTM3U header (x-tvg-url / url-tvg).</param>
    /// <returns>The entries, in playlist order.</returns>
    public static List<M3uEntry> Parse(TextReader reader, List<string> guideUrls)
    {
        var result = new List<M3uEntry>();
        M3uEntry? pending = null;
        string? pendingGroup = null;
        string? line;

        while ((line = reader.ReadLine()) is not null)
        {
            line = line.Trim().TrimStart('\uFEFF');
            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith("#EXTM3U", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var (key, value) in ParseAttributes(line))
                {
                    if (key.Equals("x-tvg-url", StringComparison.OrdinalIgnoreCase) || key.Equals("url-tvg", StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (var guideUrl in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        {
                            guideUrls.Add(guideUrl);
                        }
                    }
                }

                continue;
            }

            if (line.StartsWith("#EXTINF", StringComparison.OrdinalIgnoreCase))
            {
                pending = ParseExtInf(line);
                if (pendingGroup is not null && pending.Group.Length == 0)
                {
                    pending.Group = pendingGroup;
                }

                continue;
            }

            if (line.StartsWith("#EXTGRP:", StringComparison.OrdinalIgnoreCase))
            {
                pendingGroup = line[8..].Trim();
                if (pending is not null && pending.Group.Length == 0)
                {
                    pending.Group = pendingGroup;
                }

                continue;
            }

            if (line.StartsWith("#EXTVLCOPT:", StringComparison.OrdinalIgnoreCase))
            {
                pending ??= new M3uEntry();
                var opt = line[11..];
                var eq = opt.IndexOf('=', StringComparison.Ordinal);
                if (eq > 0)
                {
                    var key = opt[..eq].Trim();
                    var value = opt[(eq + 1)..].Trim();
                    if (key.Equals("http-user-agent", StringComparison.OrdinalIgnoreCase))
                    {
                        pending.Headers["User-Agent"] = value;
                    }
                    else if (key.Equals("http-referrer", StringComparison.OrdinalIgnoreCase) || key.Equals("http-referer", StringComparison.OrdinalIgnoreCase))
                    {
                        pending.Headers["Referer"] = value;
                    }
                }

                continue;
            }

            if (line.StartsWith('#'))
            {
                continue;
            }

            // A stream URL. Kodi-style "url|User-Agent=x&Referer=y" header suffixes are supported.
            var entry = pending ?? new M3uEntry();
            var url = line;
            var pipe = url.IndexOf('|', StringComparison.Ordinal);
            if (pipe > 0)
            {
                foreach (var pair in url[(pipe + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
                {
                    var eq = pair.IndexOf('=', StringComparison.Ordinal);
                    if (eq > 0)
                    {
                        entry.Headers[Uri.UnescapeDataString(pair[..eq])] = Uri.UnescapeDataString(pair[(eq + 1)..]);
                    }
                }

                url = url[..pipe];
            }

            entry.Url = url;
            if (entry.Name.Length == 0)
            {
                entry.Name = entry.TvgName.Length > 0 ? entry.TvgName : url;
            }

            result.Add(entry);
            pending = null;
            pendingGroup = null;
        }

        return result;
    }

    private static M3uEntry ParseExtInf(string line)
    {
        var entry = new M3uEntry();

        // The display name follows the first comma that is not inside a quoted attribute value.
        var inQuotes = false;
        var comma = -1;
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (line[i] == ',' && !inQuotes)
            {
                comma = i;
                break;
            }
        }

        var header = comma >= 0 ? line[..comma] : line;
        entry.Name = comma >= 0 ? line[(comma + 1)..].Trim() : string.Empty;

        foreach (var (key, value) in ParseAttributes(header))
        {
            switch (key.ToLowerInvariant())
            {
                case "tvg-id":
                    entry.TvgId = value.Trim();
                    break;
                case "tvg-name":
                    entry.TvgName = value.Trim();
                    break;
                case "tvg-logo":
                    entry.Logo = value.Trim();
                    break;
                case "group-title":
                    entry.Group = value.Trim();
                    break;
                case "tvg-chno":
                case "channel-number":
                case "tvg-num":
                    entry.Number = value.Trim();
                    break;
                case "http-user-agent":
                case "user-agent":
                    entry.Headers["User-Agent"] = value;
                    break;
                case "http-referrer":
                case "http-referer":
                    entry.Headers["Referer"] = value;
                    break;
            }
        }

        return entry;
    }

    private static IEnumerable<(string Key, string Value)> ParseAttributes(string text)
    {
        foreach (Match m in AttributeRegex().Matches(text))
        {
            yield return (m.Groups[1].Value, m.Groups[2].Value);
        }
    }
}
