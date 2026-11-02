---
title: Settings
nav_order: 9
wiki_title: Settings
---

# Settings

All settings are on the plugin's page: **Dashboard → Plugins → IPTV Hub**.

## Settings tab

| Setting | Default | What it does |
|---|---|---|
| **Add channels and guide to Jellyfin Live TV** | On | Creates one M3U tuner per playlist and an XMLTV guide in Jellyfin Live TV, and tags Live TV channels with their categories. Turning it off removes the tuners and guide the plugin added. |
| **Show raw playlist groups as folders** | On | Adds the playlist's own groups as extra folders in the IPTV Hub channel. Only used when automatic categories are off, since otherwise the groups already are categories. |
| **Show an "All Channels" folder** | On | Adds an *All Channels* folder after the category folders. If nothing else would be shown, it appears anyway. |
| **Guide days to keep** | 3 | How many days ahead of guide data to keep (1–14). More days means a bigger guide and slower Live TV guide refreshes. |
| **Download playlists/guides again after (hours)** | 12 | How long a downloaded copy is reused before downloading again. It's also the default interval of the scheduled task (see below). |

## Other tabs

| Setting | Tab | Default |
|---|---|---|
| **Also load guide URLs advertised by playlists (`x-tvg-url`)** | Guides (EPG) | On |
| **Automatically make each playlist group a category** | Categories | On |

## Buttons

- **Save** stores your changes and rebuilds from cached downloads. Nothing is downloaded unless it's older than the refresh interval.
- **Save & re-download now** stores your changes and downloads every URL source again.

## Scheduled task

**Refresh IPTV playlists and guides** (under **Dashboard → Scheduled Tasks → Live TV**) downloads all sources again. It runs every 12 hours by default. Change its schedule there; the *Download again after* setting only sets the starting schedule when the plugin is first installed.

## Where files are stored

Everything lives in the plugin's data folder. On Docker that's `/config/plugins/Jellyfin.Plugin.IptvHub/`.

| Path | Contents |
|---|---|
| `cache/` | Downloaded playlists and guides (last good copy). |
| `uploads/` | Files uploaded through the plugin page. |
| `livetv/playlist-<id>.m3u` | Generated playlist for each source's Live TV tuner. |
| `livetv/guide.xml` | Generated, filtered guide used by Live TV and the [exports](exports.md). |
