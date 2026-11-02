---
title: Exports & API
nav_order: 10
wiki_title: Exports-and-API
---

# Exports & API

## Merged playlist and guide for other players

IPTV Hub serves your cleaned-up channel list and matching guide, so other IPTV players (TiviMate, VLC, Kodi, …) can use them:

| URL | Contents |
|---|---|
| `http://<server>:8096/IptvHub/playlist.m3u?ApiKey=<key>` | All visible channels with your names, numbers, logos and categories (`group-title`). Per-channel User-Agent/Referer are included as `#EXTVLCOPT` lines. |
| `http://<server>:8096/IptvHub/playlist.m3u?category=<name or id>&ApiKey=<key>` | Only one category, by custom category id or playlist group name. |
| `http://<server>:8096/IptvHub/epg.xml?ApiKey=<key>` | The filtered XMLTV guide whose channel ids match the playlist's `tvg-id`s. |

Create an API key under **Dashboard → API Keys**.

The playlist uses the original stream URLs. Players stream straight from your provider, not through Jellyfin.

## Admin API

The plugin page uses these endpoints. They need an administrator's token.

| Method & path | Purpose |
|---|---|
| `GET /IptvHub/Status` | Last refresh time, channel / guide / programme counts, errors. |
| `GET /IptvHub/Channels` | Every channel with its key, names, group, guide match, categories, hidden flag and what's on now. |
| `GET /IptvHub/Categories` | Custom categories and playlist groups, with channel counts. |
| `POST /IptvHub/Refresh?force=true` | Reload everything; `force=true` downloads all sources again. |
| `POST /IptvHub/Upload` | Upload a playlist or guide file (multipart field `file`, up to 1 GB). Returns its server path. |
