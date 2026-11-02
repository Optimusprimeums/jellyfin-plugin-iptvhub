---
title: Watching
nav_order: 8
wiki_title: Watching
---

# Watching

IPTV Hub gives you two ways to watch, and you can use both.

| | IPTV Hub folders | Live TV |
|---|---|---|
| Where | *My Media → IPTV Hub* | *Live TV* |
| Grouped by category | Yes, one folder per category | No: Jellyfin's Live TV list is always flat (see below) |
| Guide grid | No; "Now" and "Next" in each channel's description | Yes |
| Recordings / DVR | No | Yes |
| Stream sharing between viewers | No | Yes |
| Works in native TV apps (Android TV / NVIDIA Shield, …) | Yes | Yes |

## IPTV Hub folders

Open **My Media → IPTV Hub** in any Jellyfin app. You get:

- one folder per visible [category](categories.md) that has channels, in your order;
- **All Channels**, unless turned off in [Settings](settings.md).

Each channel shows its logo, number and name. Its description shows the programme on now (with end time and synopsis) and the one after it. The description updates about every 10 minutes.

{: .note }
Jellyfin only shows a plugin's channel after its **Refresh Channels** task has run, which happens every 24 hours. Right after installing, run **Dashboard → Scheduled Tasks → Channels → Refresh Channels** yourself.

If you don't see IPTV Hub under *My Media*, also check **Dashboard → Users → (user) → Access**. Make sure **Enable access to all channels** or *IPTV Hub* is ticked.

## Live TV

With **Add channels and guide to Jellyfin Live TV** on (the default, see [Settings](settings.md)), the plugin:

- creates one M3U tuner per playlist, named `IPTV Hub - <playlist name>`, under **Dashboard → Live TV**;
- registers its filtered guide as an XMLTV guide provider;
- starts Jellyfin's **Refresh Guide** task whenever the published channels or guide change.

Playback, transcoding, recordings and stream sharing all go through Jellyfin's own M3U tuner, so they behave like any other Live TV source. The tuner's settings, such as the tuner limit or hardware transcoding, can be changed in **Dashboard → Live TV** and are kept by the plugin.

Hidden channels are left out of Live TV. They disappear after the next guide refresh.

### Categories in Live TV

Jellyfin's Live TV screen is always one flat channel list, and a plugin can't add folders to it. To make categories usable anyway, IPTV Hub **tags every Live TV channel with its categories**:

- In the **web app** (and apps built on it, such as Jellyfin Media Player and the Android phone app), open a channel's page and click a tag. That lists every channel in that category.
- The tags are refreshed after every plugin refresh and every guide refresh. The log shows a line like `IPTV Hub: 4111 of 4111 Live TV channels are from IPTV Hub; updated category tags on 12`.
- **Native TV apps**, such as **Jellyfin for Android TV** (including the NVIDIA Shield), Swiftfin and Roku, don't show channel tags on their Live TV screens. In those apps, use the **IPTV Hub folders** to browse by category.

## Large playlists

Thousands of channels make everything slower, from the IPTV Hub folders to the Live TV guide refresh (about 11 minutes for 4,000 channels). Trimming the list helps the most:

- pick fewer channels in your provider's portal, if it allows that;
- or tick **Hide channels** on categories you never watch (see [Categories](categories.md)).
