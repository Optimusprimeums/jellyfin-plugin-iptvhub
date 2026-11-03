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

## Continuous integration

`.github/workflows/build.yml` builds and tests every push and pull request, and attaches the plugin zip as a workflow artifact.

## Releasing

Releases are driven by `v*` tags, and **CI keeps `manifest.json` in step with the release**. Jellyfin rejects a download whose MD5 doesn't match the manifest, so don't edit release entries in the manifest by hand.

1. Bump `AssemblyVersion` / `FileVersion` in the `.csproj` and push to `master`.
2. Create a GitHub release with the tag `vx.y.z.w`, using its notes for the changes. Either:
   - leave it without files, and CI builds the zip and attaches it; or
   - attach your own `IptvHub_x.y.z.w.zip` (from `./build.ps1`). CI **never replaces** a zip that's already attached.
3. CI then downloads the zip as published, and adds or corrects the version's entry in `manifest.json`:
   - MD5 checksum, download URL and timestamp come from that published zip;
   - the changelog is the first line of the release notes;
   - `targetAbi` is `12.4.0.0`.

   CI commits the result to `master` and uploads an `MD5SUMS.txt` beside the zip.
4. Servers with the repository added see the update in their catalog.

To re-check an existing release, run **Actions → Build → Run workflow** with its tag (e.g. `v1.2.1.0`). This keeps the release's zip and only fixes `manifest.json` if its entry doesn't match.

## Docs

This site is built by GitHub Pages from the `docs/` folder. The [wiki](https://github.com/Optimusprimeums/jellyfin-plugin-iptvhub/wiki) is generated from the same files:

```powershell
python scripts/sync_wiki.py <path-to-wiki-checkout>
```

Edit the files in `docs/` only; wiki edits are overwritten on the next sync.
