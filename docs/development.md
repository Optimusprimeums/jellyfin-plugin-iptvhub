---
title: Development
nav_order: 12
wiki_title: Development
---

# Development

## Building

You need the **.NET 10 SDK**.

```powershell
./build.ps1 -Version 1.2.1.0 -Changelog "What changed"
```

This produces:

- `dist/IptvHub_<version>/`, containing `Jellyfin.Plugin.IptvHub.dll` and `meta.json`;
- `dist/IptvHub_<version>.zip`;
- the zip's MD5 checksum, printed for `manifest.json`.

## Tests

```powershell
dotnet test
```

The parser and category tests always run. The end-to-end test in `Jellyfin.Plugin.IptvHub.Tests/PipelineTests.cs` runs only when `IPTVHUB_TESTDATA` points at a folder containing:

- `local/local.m3u` and `local/guide.xml`;
- and an HTTP server on `127.0.0.1:8099` serving `remote.m3u` and `guide.xml.gz`.

The end-to-end test runs the whole refresh against fake Jellyfin services: playlist and guide loading, matching, categories, the Live TV registration, the channel folders, and tagging.

## How it fits together

| Part | Role |
|---|---|
| `Services/IptvManager` | Loads playlists and guides, matches channels to the guide, applies overrides and categories, and writes the files for Live TV. |
| `Services/SourceFetcher` | Downloads and caches remote sources, keeping the last good copy. |
| `Services/LiveTvPublisher` | Creates or removes the M3U tuners and XMLTV guide, and starts **Refresh Guide** when output changes. |
| `Services/LiveTvTagger` | Tags Jellyfin's Live TV channels with their categories. It finds them by Jellyfin's M3U channel id: `m3u_` + MD5(tuner playlist path) + MD5(stream URL). |
| `Channels/IptvHubChannel` | The browsable *IPTV Hub* channel with category folders. |
| `Api/IptvHubController` | Endpoints for the config page, plus the [exports](exports.md). |
| `Configuration/configPage.html` | The plugin's dashboard page. |

## Releasing

1. Bump `AssemblyVersion` / `FileVersion` in the `.csproj`, then run `./build.ps1 -Version x.y.z.w -Changelog "…"`.
2. Create a GitHub release `vx.y.z.w` and attach the zip.
3. Add an entry at the **top** of `versions` in `manifest.json`, with:
   - the version;
   - the release download URL;
   - the MD5 checksum;
   - a UTC timestamp;
   - `targetAbi` `12.4.0.0`.
4. Push. Servers with the repository added see the update in their catalog.

## Docs

This site is built by GitHub Pages from the `docs/` folder. The [wiki](https://github.com/Optimusprimeums/jellyfin-plugin-iptvhub/wiki) is generated from the same files:

```powershell
python scripts/sync_wiki.py <path-to-wiki-checkout>
```

Edit the files in `docs/` only; wiki edits are overwritten on the next sync.
