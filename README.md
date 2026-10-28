# IPTV Hub for Jellyfin

A Jellyfin plugin that serves IPTV from M3U playlists and XMLTV program guides. You can sort channels into your own categories, and the playlists' own categories are kept too.

Built for **Jellyfin 12.4** (.NET 10).

## Features

- **Playlists (M3U / M3U8)** from:
  - a **URL** (http/https; downloaded and cached, with an optional custom User-Agent)
  - a **file path on the Jellyfin server** (e.g. `/media/iptv/list.m3u`, `D:\iptv\list.m3u`)
  - an **upload** from your browser on the config page (saved into the plugin's data folder)
  - gzipped playlists work too
  - supports `tvg-id`, `tvg-name`, `tvg-logo`, `group-title`, `tvg-chno`, `#EXTGRP`, `#EXTVLCOPT` (user-agent/referrer) and Kodi-style `url|User-Agent=…` headers
- **Electronic program guides (XMLTV)**: `.xml` or `.xml.gz`, from a URL, a server path or an upload
  - guide URLs that a playlist advertises (`x-tvg-url` / `url-tvg`) are loaded automatically (you can turn this off)
  - channels are matched by `tvg-id` first, then by name ("BBC One HD" matches "BBC One"); you can override the match per channel
  - big guides are streamed, and only the channels you actually have are kept
- **Categories**
  - **Automatic**: each playlist `group-title` becomes a category with its channels already in it. Nothing to tick. New groups that appear later are added too.
  - **Tidy them up** on the Categories tab:
    - rename, reorder (▲/▼), **merge** several groups into one category, or add more groups to a category
    - untick **Show** to hide a category, or tick **Hide channels** to hide every channel in it (handy for groups you never watch)
    - a deleted auto-created category stays deleted
  - **Extra categories** of your own, filled by playlist groups, by picking channels on the Channels tab, or by a channel-name regex (e.g. `sport|espn`)
  - a channel can be in any number of categories
- **Per-channel overrides**: rename, renumber, hide, set the guide id
- **Two ways to watch**
  1. **IPTV Hub channel** (Home → Channels → IPTV Hub): a folder per category in your order, then All Channels. Each channel shows what's on now and next.
  2. **Jellyfin Live TV** (optional, on by default): the plugin creates one M3U tuner per playlist and one XMLTV guide provider. You get the full guide grid, recordings and stream sharing. The tuner's channel group is the first custom category, otherwise the playlist group.
- **Merged outputs** for other players: `/IptvHub/playlist.m3u` (optional `?category=<id or group name>`) and `/IptvHub/epg.xml`. Authenticate with `api_key`.
- Scheduled task **"Refresh IPTV playlists and guides"** (Dashboard → Scheduled Tasks → Live TV), every 12 h by default.

## Install from the Jellyfin dashboard

1. Go to **Dashboard → Plugins → Repositories** (on older layouts: Plugins → Catalog → ⚙ Settings) and add a repository:
   - Name: `IPTV Hub`
   - URL: `https://raw.githubusercontent.com/Optimusprimeums/jellyfin-plugin-iptvhub/master/manifest.json`
2. Open the **Catalog**, find **IPTV Hub** under *Live TV*, and click **Install**.
3. Restart Jellyfin, then open **Dashboard → Plugins → IPTV Hub**.

Updates will then appear in the catalog.

## Install manually

1. Build it (needs the .NET 10 SDK):
   ```powershell
   ./build.ps1
   ```
   This produces `dist/IptvHub_1.0.0.0/` (containing `Jellyfin.Plugin.IptvHub.dll` and `meta.json`) and a zip of the same files.
2. Copy the `IptvHub_1.0.0.0` folder into your Jellyfin plugins directory:
   - Windows: `%ProgramData%\Jellyfin\Server\plugins\`
   - Linux: `/var/lib/jellyfin/plugins/`
   - Docker: `/config/plugins/`
3. Restart Jellyfin.
4. Open **Dashboard → Plugins → IPTV Hub**.

## Use

1. **Playlists** tab: click **+ URL** or **+ Server path**, or **Upload file…**. Then click **Save**.
2. **Guides (EPG)** tab: add XMLTV sources the same way.
3. **Categories** tab: after the first save your playlist groups are already categories. Rename, reorder, merge or hide them as you like.
4. **Channels** tab (optional): put individual channels into extra categories, or hide, rename or renumber channels, or set a guide id.
5. **Save** applies your changes from cached downloads. **Save & re-download now** fetches every URL again.

The status line at the top shows channel and guide counts, plus any errors (bad URL, missing file, invalid regex…).

## How it works

```
playlists (URL/file) ──┐                      ┌─► IPTV Hub channel (category folders, now/next)
                       ├─► IptvManager ───────┤
XMLTV guides ──────────┘   parse, match EPG,  ├─► <data>/plugins/configurations/…/livetv/playlist-*.m3u ─► M3U tuners
                           apply categories   └─► <data>/…/livetv/guide.xml ─────────────────────────────► XMLTV provider
```

The plugin keeps downloads in its data folder (`cache/`) and uses the last good copy if a download fails. Live TV playback goes through Jellyfin's own M3U tuner, so transcoding, stream sharing and recording work as usual.

## Releasing a new version

1. `./build.ps1 -Version 1.2.0.0 -Changelog "…"` (it prints the zip's MD5).
2. Create a GitHub release `v1.2.0.0` and attach `dist/IptvHub_1.2.0.0.zip`.
3. Add a new entry at the **top** of `versions` in `manifest.json`: the version, the release download URL, the MD5 checksum and a UTC timestamp. Then push it.

## Development

```powershell
dotnet test                      # parser tests
$env:IPTVHUB_TESTDATA = "…"      # optional end-to-end test (see Jellyfin.Plugin.IptvHub.Tests/PipelineTests.cs)
```
