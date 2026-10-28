using System;
using System.Collections.Generic;
using System.Globalization;
using Jellyfin.Plugin.IptvHub.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.IptvHub;

/// <summary>
/// IPTV Hub: M3U playlists + XMLTV guides with categories.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public override string Name => "IPTV Hub";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("7c1f6a52-3e1b-4c8e-9a0d-5b2f4e8d9c31");

    /// <inheritdoc />
    public override string Description => "Serve IPTV from M3U playlists (URL or local file) with XMLTV guides and custom categories.";

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.Configuration.configPage.html", GetType().Namespace)
            }
        };
    }
}
