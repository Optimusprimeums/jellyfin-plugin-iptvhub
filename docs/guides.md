---
title: Guides (EPG)
nav_order: 5
wiki_title: Guides
---

# Guides (EPG)

The **Guides (EPG)** tab lists your XMLTV sources. A guide fills Jellyfin's Live TV guide grid, and adds the "Now" and "Next" lines to the IPTV Hub folders.

## Adding a guide

Use **+ URL**, **+ Server path** or **Upload file…**, exactly as for [playlists](playlists.md). Both `.xml` and gzipped `.xml.gz` files work; compression is detected automatically.

**Also load guide URLs advertised by playlists (`x-tvg-url`)** is on by default. If a playlist's header names one or more guide URLs, they're loaded as well, without you adding them.

## How channels are matched to the guide

Each channel's guide is found in this order:

1. **Guide id override:** the id you typed in the channel's *Guide id* box on the [Channels](channels.md) tab.
2. **`tvg-id`:** the playlist's `tvg-id` matches a guide channel id. Case doesn't matter.
3. **Name:** the channel's `tvg-id`, its original name, then its current name are compared with the guide's channel names and ids. Names are compared loosely:
   - case, spaces and punctuation are ignored;
   - a trailing quality tag (`HD`, `FHD`, `UHD`, `SD`, `4K`, `HEVC`, `H265`) is ignored.

   So "BBC One HD" matches "bbc.one".

On the **Channels** tab, filter by **No guide** to see unmatched channels. To fix one, type the correct guide channel id into its **Guide id** box and click **Save**.

## What's kept

- Only programmes for channels you actually have are kept, so large guides with tens of thousands of channels are fine.
- Programmes from 6 hours ago up to *Guide days to keep* ahead (default 3, maximum 14) are kept. See [Settings](settings.md).
- If two guides list the same programme (same channel, same start time), the first guide's copy is used.

The result is written to a single filtered guide file, which the plugin registers as Jellyfin's XMLTV guide for Live TV. After each refresh it clears Jellyfin's cached copy, so Live TV picks up the new data on the next guide refresh.
