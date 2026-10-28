using System;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.IptvHub.Services;
using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.IptvHub.Api;

/// <summary>
/// Endpoints used by the configuration page, plus the merged playlist / guide for external players.
/// </summary>
[ApiController]
[Route("IptvHub")]
public class IptvHubController : ControllerBase
{
    private const long MaxUploadBytes = 1024L * 1024 * 1024;

    private readonly IptvManager _manager;

    public IptvHubController(IptvManager manager)
    {
        _manager = manager;
    }

    /// <summary>Refresh status and errors.</summary>
    /// <returns>Status.</returns>
    [HttpGet("Status")]
    [Authorize(Policy = Policies.RequiresElevation)]
    public ActionResult GetStatus()
    {
        var s = _manager.Snapshot;
        return Ok(new
        {
            Refreshing = _manager.IsRefreshing,
            LastRefresh = s.BuiltUtc == default ? (DateTime?)null : s.BuiltUtc,
            Channels = s.Channels.Count,
            Hidden = s.Channels.Count(c => c.Hidden),
            WithGuide = s.Channels.Count(c => c.EpgId.Length > 0),
            GuideChannels = s.EpgChannelCount,
            Programmes = s.ProgrammeCount,
            s.Errors
        });
    }

    /// <summary>All channels with their categories.</summary>
    /// <returns>Channels.</returns>
    [HttpGet("Channels")]
    [Authorize(Policy = Policies.RequiresElevation)]
    public ActionResult GetChannels()
    {
        var s = _manager.Snapshot;
        return Ok(s.Channels.Select(c => new
        {
            c.Key,
            c.Name,
            c.OriginalName,
            c.Number,
            c.Group,
            c.SourceName,
            c.TvgId,
            c.EpgId,
            c.Logo,
            c.Hidden,
            c.CategoryIds,
            NowPlaying = s.GetNowNext(c, DateTime.UtcNow).Now?.Title
        }));
    }

    /// <summary>Custom and playlist categories with channel counts.</summary>
    /// <returns>Categories.</returns>
    [HttpGet("Categories")]
    [Authorize(Policy = Policies.RequiresElevation)]
    public ActionResult GetCategories()
    {
        var s = _manager.Snapshot;
        return Ok(new { Custom = s.CustomCategories, Playlist = s.PlaylistGroups });
    }

    /// <summary>Reload all sources.</summary>
    /// <param name="force">Re-download remote sources even if the cache is fresh.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Status.</returns>
    [HttpPost("Refresh")]
    [Authorize(Policy = Policies.RequiresElevation)]
    public async Task<ActionResult> Refresh([FromQuery] bool force = true, CancellationToken cancellationToken = default)
    {
        await _manager.RefreshAsync(force, cancellationToken).ConfigureAwait(false);
        return GetStatus();
    }

    /// <summary>Upload a playlist or guide file to the server.</summary>
    /// <param name="file">The file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The server-side path to use as a File source.</returns>
    [HttpPost("Upload")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [RequestSizeLimit(MaxUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadBytes)]
    public async Task<ActionResult> Upload(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest("No file.");
        }

        var safeName = string.Concat(Path.GetFileName(file.FileName).Select(ch => char.IsLetterOrDigit(ch) || ch is '.' or '-' or '_' ? ch : '_'));
        if (safeName.Trim('.').Length == 0)
        {
            safeName = "upload.dat";
        }

        var dir = Path.Combine(Plugin.Instance!.DataFolderPath, "uploads");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, safeName);
        await using (var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await file.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
        }

        return Ok(new { Path = path, Name = Path.GetFileNameWithoutExtension(safeName) });
    }

    /// <summary>
    /// Merged playlist of all visible channels (for other IPTV players). Authenticate with ?api_key=.
    /// </summary>
    /// <param name="category">Optional custom category id or playlist group name to filter by.</param>
    /// <returns>M3U.</returns>
    [HttpGet("playlist.m3u")]
    [Authorize]
    [Produces("audio/x-mpegurl")]
    public ActionResult GetPlaylist([FromQuery] string? category = null)
    {
        var s = _manager.Snapshot;
        var channels = s.Channels.Where(c => !c.Hidden);
        if (!string.IsNullOrEmpty(category))
        {
            channels = channels.Where(c => c.CategoryIds.Contains(category) || string.Equals(c.Group, category, StringComparison.OrdinalIgnoreCase));
        }

        var names = Plugin.Instance!.Configuration.Categories.ToDictionary(c => c.Id, c => c.Name);
        var text = IptvManager.WritePlaylist(null, channels, names, true);
        return File(Encoding.UTF8.GetBytes(text), "audio/x-mpegurl", "iptvhub.m3u");
    }

    /// <summary>The filtered XMLTV guide matching <c>playlist.m3u</c>.</summary>
    /// <returns>XMLTV.</returns>
    [HttpGet("epg.xml")]
    [Authorize]
    [Produces(MediaTypeNames.Application.Xml)]
    public ActionResult GetGuide()
    {
        var path = IptvManager.GuidePath;
        if (!System.IO.File.Exists(path))
        {
            return NotFound();
        }

        return PhysicalFile(path, MediaTypeNames.Application.Xml, "epg.xml");
    }
}
