using System;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.IptvHub.Configuration;

/// <summary>
/// Where a playlist or guide comes from.
/// </summary>
public enum SourceKind
{
    /// <summary>Downloaded over HTTP(S).</summary>
    Url = 0,

    /// <summary>A file on the Jellyfin server's disk (or one uploaded through the config page).</summary>
    File = 1
}

/// <summary>
/// An M3U / M3U8 playlist.
/// </summary>
public class PlaylistSource
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = string.Empty;

    public SourceKind Kind { get; set; } = SourceKind.Url;

    /// <summary>Gets or sets the URL or the server-side file path.</summary>
    public string Location { get; set; } = string.Empty;

    /// <summary>Gets or sets an optional User-Agent used to fetch the playlist and its streams.</summary>
    public string UserAgent { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;
}

/// <summary>
/// An XMLTV electronic program guide (.xml or .xml.gz).
/// </summary>
public class EpgSource
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = string.Empty;

    public SourceKind Kind { get; set; } = SourceKind.Url;

    public string Location { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;
}

/// <summary>
/// A user-defined category. Channels join it either explicitly or by matching a rule.
/// </summary>
public class CustomCategory
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the channel keys explicitly assigned to this category.</summary>
    public string[] ChannelKeys { get; set; } = Array.Empty<string>();

    /// <summary>Gets or sets the playlist group-titles whose channels belong to this category.</summary>
    public string[] Groups { get; set; } = Array.Empty<string>();

    /// <summary>Gets or sets a value indicating whether the category is left out of browsing and Live TV grouping.</summary>
    public bool Hidden { get; set; }

    /// <summary>Gets or sets a value indicating whether every channel in this category is hidden.</summary>
    public bool HideChannels { get; set; }

    /// <summary>Gets or sets an optional regex; channels whose name matches are included automatically.</summary>
    public string NamePattern { get; set; } = string.Empty;

    /// <summary>Gets or sets an optional regex; channels whose M3U group-title matches are included automatically.</summary>
    public string GroupPattern { get; set; } = string.Empty;

    public int SortOrder { get; set; }
}

/// <summary>
/// Per-channel tweaks keyed by the channel's stable key.
/// </summary>
public class ChannelOverride
{
    public string Key { get; set; } = string.Empty;

    public bool Hidden { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Number { get; set; } = string.Empty;

    /// <summary>Gets or sets an EPG channel id that replaces the playlist's tvg-id.</summary>
    public string EpgId { get; set; } = string.Empty;
}

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    public PlaylistSource[] Playlists { get; set; } = Array.Empty<PlaylistSource>();

    public EpgSource[] EpgSources { get; set; } = Array.Empty<EpgSource>();

    public CustomCategory[] Categories { get; set; } = Array.Empty<CustomCategory>();

    public ChannelOverride[] Overrides { get; set; } = Array.Empty<ChannelOverride>();

    /// <summary>Gets or sets a value indicating whether each new playlist group-title automatically becomes a category.</summary>
    public bool AutoCreateGroupCategories { get; set; } = true;

    /// <summary>Gets or sets the playlist groups already turned into categories, so deleted ones are not re-created.</summary>
    public string[] KnownGroups { get; set; } = Array.Empty<string>();

    /// <summary>Gets or sets a value indicating whether raw playlist groups are shown as folders (only used when automatic categories are off).</summary>
    public bool ShowPlaylistGroups { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether an "All Channels" folder is shown.</summary>
    public bool ShowAllChannelsFolder { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the merged playlist and guide are registered with Jellyfin Live TV.</summary>
    public bool RegisterWithLiveTv { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether guide URLs advertised by playlists (x-tvg-url) are loaded automatically.</summary>
    public bool UsePlaylistGuideUrls { get; set; } = true;

    /// <summary>Gets or sets how many days of guide data are kept.</summary>
    public int GuideDays { get; set; } = 3;

    /// <summary>Gets or sets the automatic refresh interval in hours.</summary>
    public int RefreshIntervalHours { get; set; } = 12;
}
