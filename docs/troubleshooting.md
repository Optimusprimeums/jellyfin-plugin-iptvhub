---
title: Troubleshooting
nav_order: 11
wiki_title: Troubleshooting
---

# Troubleshooting

Start with the **status line** at the top of the plugin page. It shows the channel and guide counts and any errors (bad URL, missing file, invalid pattern…). For more detail, open **Dashboard → Logs** and search for lines starting with `IPTV Hub:`.

## IPTV Hub isn't in the plugin catalog

- Your server must be **Jellyfin 12.4 or later**. Check **Dashboard → General → Server version**. Older servers hide the plugin without any message.
- Check the repository URL is exactly `https://raw.githubusercontent.com/Optimusprimeums/jellyfin-plugin-iptvhub/master/manifest.json`, and that it's enabled.
- Look in the log for `Failed to deserialize the plugin manifest` or download errors mentioning that URL.

## The IPTV Hub folders don't appear under My Media

1. Run **Dashboard → Scheduled Tasks → Channels → Refresh Channels**. Jellyfin only adds a plugin's channel when this task runs, and on its own that's once every 24 hours.
2. Check **Dashboard → Users → (user) → Access**. Make sure **Enable access to all channels** or *IPTV Hub* is ticked.
3. Restart the app on the device.

## Live TV channels or guide are missing

- The status line should list channels and "with guide data". If channels show 0, check the playlist URL and User-Agent.
- After changes, the plugin starts **Refresh Guide** for you. With thousands of channels it can take 10+ minutes. Watch **Dashboard → Scheduled Tasks → Live TV → Refresh Guide**.
- **Dashboard → Live TV** should list a tuner named `IPTV Hub - <playlist>` and an XMLTV guide whose path ends in `livetv/guide.xml`. If they're missing, make sure **Add channels and guide to Jellyfin Live TV** is ticked on the Settings tab, then click Save.

## A channel has no guide data

On the **Channels** tab, filter **Guide → No guide**. For each channel:

- Find the channel's id in your XMLTV file (the `id` of its `<channel>` element).
- Type it into the channel's **Guide id** box and click **Save**.

See [Guides](guides.md) for how automatic matching works.

## Live TV channels have no category tags

- Tags are only added when **Add channels and guide to Jellyfin Live TV** is on.
- After a guide refresh, the log should show `IPTV Hub: N of M Live TV channels are from IPTV Hub; updated category tags on …`. If N is 0, open an issue and attach the log.
- Native TV apps (Android TV / NVIDIA Shield, Swiftfin, Roku) don't show channel tags; use the IPTV Hub folders there. See [Watching](watching.md).

## A channel won't play

- Try the stream URL in VLC on the server's network. Many providers allow only a limited number of streams at once, or only certain User-Agents.
- If the provider needs a User-Agent, set it on the playlist (see [Playlists](playlists.md)).
- In the log, look for `Error opening tuner` or ffmpeg errors near the time you tried to play.

## Everything is slow

Thousands of channels make browsing and guide refreshes slow. Trim the playlist at the provider, or hide whole categories with **Hide channels** (see [Categories](categories.md)).

## Downloads fail

If a download fails, the plugin keeps using the last good copy and shows the error in the status line. Check that the URL opens from the Jellyfin server itself, not only from your browser, and that any User-Agent the provider requires is set.

## Reporting a problem

Open an issue on [GitHub](https://github.com/Optimusprimeums/jellyfin-plugin-iptvhub/issues) with:

- your Jellyfin and IPTV Hub versions;
- what you did and what you expected;
- the `IPTV Hub:` lines from the log, plus any errors around them. Remove your playlist URLs and credentials first.
