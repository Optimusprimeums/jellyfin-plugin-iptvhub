---
title: Playlists
nav_order: 4
wiki_title: Playlists
---

# Playlists

The **Playlists** tab lists your M3U / M3U8 sources. You can add as many as you like; each gets its own Live TV tuner.

## Adding a playlist

| Button | Use it for |
|---|---|
| **+ URL** | A playlist on the web (`http://` or `https://`). The plugin downloads it and keeps a cached copy. |
| **+ Server path** | A file already on the Jellyfin server, e.g. `/media/iptv/list.m3u` or `D:\iptv\list.m3u`. The path is as the *server* sees it; on Docker, that's the path inside the container. |
| **Upload file…** | A file on the computer you're browsing from. It's uploaded to the plugin's data folder and added as a server-path source. |

Each playlist has:

- **On:** untick to skip the playlist without deleting it.
- **Name:** shown in the channel list and as the Live TV tuner name (`IPTV Hub - <name>`).
- **User-Agent** (optional): sent when downloading the playlist and when playing its streams. Some providers require a specific one.

Click **Save** to load your changes, or **Save & re-download now** to fetch every URL again immediately.

## Prefer the provider's link over a downloaded copy

Many providers rotate stream URLs or tokens. A downloaded copy then stops working without warning. If you want fewer channels, trim the list on the provider's side where possible, and keep using the URL source.

## When playlists are downloaded

- When the plugin starts, a cached copy younger than the refresh interval is reused. Otherwise the playlist is downloaded.
- Every *refresh interval* (12 hours by default, see [Settings](settings.md)), the scheduled task **Refresh IPTV playlists and guides** downloads everything again.
- When you click **Save & re-download now**.

If a download fails, the last good copy is used, and the error appears in the status line.

## What's read from the playlist

| Playlist data | Used for |
|---|---|
| Channel name (after the comma in `#EXTINF`) | Channel name |
| `tvg-id` | Guide matching |
| `tvg-name` | Guide matching by name; also the name if the line has none |
| `tvg-logo` | Channel logo |
| `group-title`, or `#EXTGRP` | Playlist group, which becomes a [category](categories.md) |
| `tvg-chno`, `channel-number`, `tvg-num` | Channel number |
| `x-tvg-url` / `url-tvg` in the `#EXTM3U` header | Extra guide sources (see [Guides](guides.md)) |
| `#EXTVLCOPT:http-user-agent=…`, `#EXTVLCOPT:http-referrer=…` | Per-channel request headers |
| Kodi-style headers after the stream URL (see below) | Per-channel request headers |

Kodi-style headers follow a `|` at the end of the stream URL, for example `http://host/stream.ts|User-Agent=VLC&Referer=http%3A%2F%2Fsite`.

Gzipped playlists are detected and unpacked automatically.

## How channels are recognised

Each channel gets a stable key made from its playlist, `tvg-id` and name. Your changes to a channel (rename, number, hide, category picks) are tied to that key. They survive playlist refreshes and stream-URL changes, as long as the channel's name and `tvg-id` stay the same.
