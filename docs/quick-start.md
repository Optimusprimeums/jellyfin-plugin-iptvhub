---
title: Quick start
nav_order: 3
wiki_title: Quick-start
---

# Quick start

This gets you from a fresh install to watching channels in a few minutes.

## 1. Add your playlist

1. Open **Dashboard → Plugins → IPTV Hub**.
2. On the **Playlists** tab, click **+ URL** and paste your provider's M3U link.
3. If your provider requires it, enter a **User-Agent**.
4. Click **Save**.

The status line at the top shows how many channels loaded, plus any errors.

## 2. Add a guide

If your playlist already advertises a guide (an `x-tvg-url` in its header), the plugin loads it automatically. Skip this step and check the status line for "with guide data".

Otherwise, on the **Guides (EPG)** tab, click **+ URL**, paste an XMLTV link (`.xml` or `.xml.gz`), and click **Save**.

## 3. Check your categories

Open the **Categories** tab. Every group in your playlist is now a category with its channels in it. Reorder, rename, merge or hide them as you like, then click **Save**. See [Categories](categories.md) for more.

## 4. Watch

- **IPTV Hub folders:** run **Dashboard → Scheduled Tasks → Channels → Refresh Channels** once. Then open **My Media → IPTV Hub** in any Jellyfin app.
- **Live TV:** the plugin has already added a tuner and the guide, and started a guide refresh. Large playlists take a while; about 11 minutes for 4,000 channels. When it finishes, open **Live TV** in any Jellyfin app.

See [Watching](watching.md) for the differences between the two.

## Tip: keep the list small

Thousands of channels make browsing and guide refreshes slow. If your provider lets you pick channels, trim the playlist there. Or hide whole categories with **Hide channels** on the [Categories](categories.md) tab.
