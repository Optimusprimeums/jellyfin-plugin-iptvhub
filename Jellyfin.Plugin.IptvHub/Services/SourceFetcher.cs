using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.IptvHub.Configuration;
using MediaBrowser.Common.Net;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.IptvHub.Services;

/// <summary>
/// Resolves a playlist / guide source to a local file, downloading and caching remote ones.
/// </summary>
public class SourceFetcher
{
    private const string DefaultUserAgent = "Jellyfin-IptvHub/1.0";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SourceFetcher> _logger;

    public SourceFetcher(IHttpClientFactory httpClientFactory, ILogger<SourceFetcher> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Returns a local path for the source.
    /// </summary>
    /// <param name="cacheId">Unique id for the cache file.</param>
    /// <param name="kind">Url or File.</param>
    /// <param name="location">URL or path.</param>
    /// <param name="userAgent">Optional user agent.</param>
    /// <param name="maxAge">How long a downloaded copy stays fresh.</param>
    /// <param name="force">Download even if the cached copy is fresh.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The local file path.</returns>
    public async Task<string> GetLocalCopyAsync(
        string cacheId,
        SourceKind kind,
        string location,
        string? userAgent,
        TimeSpan maxAge,
        bool force,
        CancellationToken cancellationToken)
    {
        location = location.Trim();
        if (location.Length == 0)
        {
            throw new InvalidOperationException("No location configured.");
        }

        if (location.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            location = new Uri(location).LocalPath;
            kind = SourceKind.File;
        }

        if (kind == SourceKind.File)
        {
            if (!File.Exists(location))
            {
                throw new FileNotFoundException("File not found on the Jellyfin server: " + location, location);
            }

            return location;
        }

        var cacheDir = Path.Combine(Plugin.Instance!.DataFolderPath, "cache");
        Directory.CreateDirectory(cacheDir);
        var cacheFile = Path.Combine(cacheDir, cacheId + ".dat");
        var metaFile = cacheFile + ".src";

        var sameSource = File.Exists(cacheFile) && File.Exists(metaFile)
            && string.Equals(await File.ReadAllTextAsync(metaFile, cancellationToken).ConfigureAwait(false), location, StringComparison.Ordinal);
        if (!force && sameSource && DateTime.UtcNow - File.GetLastWriteTimeUtc(cacheFile) < maxAge)
        {
            return cacheFile;
        }

        var tempFile = cacheFile + ".tmp";
        try
        {
            _logger.LogInformation("IPTV Hub: downloading {Url}", location);
            using var request = new HttpRequestMessage(HttpMethod.Get, location);
            request.Headers.TryAddWithoutValidation("User-Agent", string.IsNullOrWhiteSpace(userAgent) ? DefaultUserAgent : userAgent);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(15));

            using var response = await _httpClientFactory.CreateClient(NamedClient.Default)
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using (var input = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false))
            await using (var output = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
            {
                await input.CopyToAsync(output, timeout.Token).ConfigureAwait(false);
            }

            File.Move(tempFile, cacheFile, true);
            await File.WriteAllTextAsync(metaFile, location, cancellationToken).ConfigureAwait(false);
            return cacheFile;
        }
        catch (Exception ex) when (sameSource && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "IPTV Hub: download of {Url} failed, using the previously downloaded copy", location);
            return cacheFile;
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
