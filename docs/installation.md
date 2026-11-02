---
title: Installation
nav_order: 2
wiki_title: Installation
---

# Installation

IPTV Hub needs **Jellyfin 12.4 or later**. On older servers the plugin is hidden from the catalog, with no error message.

## From the Jellyfin dashboard (recommended)

1. Open **Dashboard → Plugins → Repositories**. On older layouts this is Plugins → Catalog → ⚙ Settings.
2. Add a repository:
   - **Name:** `IPTV Hub`
   - **URL:** `https://raw.githubusercontent.com/Optimusprimeums/jellyfin-plugin-iptvhub/master/manifest.json`
3. Open the **Catalog**, find **IPTV Hub** under *Live TV*, and click **Install**.
4. Restart Jellyfin.
5. Open **Dashboard → Plugins → IPTV Hub** to configure it.

New versions then appear in the catalog, and under **My Plugins** when an update is available.

## Manually

1. Download `IptvHub_<version>.zip` from the [releases page](https://github.com/Optimusprimeums/jellyfin-plugin-iptvhub/releases).
2. Extract it into a new folder named `IPTV Hub_<version>` inside Jellyfin's plugins folder:

   | Install type | Plugins folder |
   |---|---|
   | Docker | `/config/plugins/` |
   | Linux package | `/var/lib/jellyfin/plugins/` |
   | Windows | `%ProgramData%\Jellyfin\Server\plugins\` |

3. Restart Jellyfin.

## After installing

Jellyfin only shows a plugin's channel under *My Media* after its **Refresh Channels** task has run. That task runs every 24 hours. To see the IPTV Hub folders straight away, go to **Dashboard → Scheduled Tasks → Channels → Refresh Channels** and click ▶ once you've added a playlist.

## Updating

Update from **Dashboard → Plugins**, then restart Jellyfin. Your playlists, guides, categories and channel changes are kept.

## Uninstalling

Before uninstalling, untick **Add channels and guide to Jellyfin Live TV** on the plugin's **Settings** tab and click **Save**. That removes the tuners and guide the plugin added to Live TV. Then uninstall from **Dashboard → Plugins** and restart Jellyfin.
