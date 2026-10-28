using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.IptvHub.Models;

/// <summary>
/// A channel parsed from a playlist (before overrides are applied).
/// </summary>
public class M3uEntry
{
    public string Name { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public string TvgId { get; set; } = string.Empty;

    public string TvgName { get; set; } = string.Empty;

    public string Logo { get; set; } = string.Empty;

    public string Group { get; set; } = string.Empty;

    public string Number { get; set; } = string.Empty;

    /// <summary>Gets HTTP headers from #EXTVLCOPT / attributes (User-Agent, Referer).</summary>
    public Dictionary<string, string> Headers { get; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// A channel after merging all sources and applying overrides and categories.
/// </summary>
public class IptvChannel
{
    /// <summary>Gets or sets a stable key used for overrides and category assignment.</summary>
    public string Key { get; set; } = string.Empty;

    public string SourceId { get; set; } = string.Empty;

    public string SourceName { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string OriginalName { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public string Logo { get; set; } = string.Empty;

    public string Number { get; set; } = string.Empty;

    /// <summary>Gets or sets the playlist's group-title.</summary>
    public string Group { get; set; } = string.Empty;

    /// <summary>Gets or sets the tvg-id from the playlist.</summary>
    public string TvgId { get; set; } = string.Empty;

    /// <summary>Gets or sets the XMLTV channel id actually used for the guide (empty when unmatched).</summary>
    public string EpgId { get; set; } = string.Empty;

    public bool Hidden { get; set; }

    /// <summary>Gets or sets the ids of custom categories the channel belongs to.</summary>
    public List<string> CategoryIds { get; set; } = new();

    public Dictionary<string, string> Headers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// An XMLTV programme.
/// </summary>
public class EpgProgramme
{
    public string ChannelId { get; set; } = string.Empty;

    public DateTime StartUtc { get; set; }

    public DateTime EndUtc { get; set; }

    public string Title { get; set; } = string.Empty;

    public string SubTitle { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public List<string> Categories { get; set; } = new();

    public string Icon { get; set; } = string.Empty;

    public string EpisodeNum { get; set; } = string.Empty;
}

/// <summary>
/// An XMLTV &lt;channel&gt; element.
/// </summary>
public class EpgChannel
{
    public string Id { get; set; } = string.Empty;

    public List<string> DisplayNames { get; set; } = new();

    public string Icon { get; set; } = string.Empty;
}

/// <summary>
/// A browsable category (custom or from a playlist's group-title).
/// </summary>
public class CategoryView
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets "custom" or "playlist".</summary>
    public string Kind { get; set; } = string.Empty;

    public int ChannelCount { get; set; }

    public bool Hidden { get; set; }
}
