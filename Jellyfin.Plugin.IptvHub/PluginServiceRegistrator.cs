using Jellyfin.Plugin.IptvHub.Channels;
using Jellyfin.Plugin.IptvHub.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.IptvHub;

/// <summary>
/// Registers the plugin's services.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<SourceFetcher>();
        serviceCollection.AddSingleton<LiveTvPublisher>();
        serviceCollection.AddSingleton<IptvManager>();
        serviceCollection.AddSingleton<LiveTvTagger>();
        serviceCollection.AddSingleton<IChannel, IptvHubChannel>();
        serviceCollection.AddHostedService<IptvStartupService>();
    }
}
