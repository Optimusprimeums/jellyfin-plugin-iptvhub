---
title: Home
nav_order: 1
wiki_title: Home
---

# IPTV Hub for Jellyfin

IPTV Hub is a Jellyfin plugin that turns M3U playlists and XMLTV program guides into something you can actually browse. Channels are grouped into categories, and the guide is matched for you.

It needs **Jellyfin 12.4** or later.

## What it does

- **Playlists from anywhere.** Add an M3U or M3U8 playlist by URL, by a path on the Jellyfin server, or by uploading a file from your browser.
- **Program guides.** Add XMLTV guides (`.xml` or `.xml.gz`) the same three ways. Guide links that a playlist advertises are picked up automatically, and channels are matched to the guide by id or by name.
- **Automatic categories.** Every `group-title` in your playlist becomes a category with its channels already in it. You can rename, reorder, merge or hide categories, or add your own.
- **Per-channel control.** Rename, renumber, hide, or fix a channel's guide match.
- **Two ways to watch:**
  - the **IPTV Hub** folders under *My Media*, with one folder per category and what's on now and next;
  - Jellyfin **Live TV**, with the full guide grid and recordings. The plugin sets up the tuner and guide for you and tags every channel with its categories.

## Where to start

1. [Install the plugin](installation.md).
2. Follow the [Quick start](quick-start.md) to add a playlist and a guide.
3. Read about [Categories](categories.md) to organise your channels.
4. See [Watching](watching.md) for what each Jellyfin app shows.

Something not working? See [Troubleshooting](troubleshooting.md).
