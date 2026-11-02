---
title: Categories
nav_order: 6
wiki_title: Categories
---

# Categories

Categories are how IPTV Hub groups channels. They become the folders in the IPTV Hub channel, the group of each Live TV channel, and the tags on Live TV channels.

## Automatic categories from your playlist

With **Automatically make each playlist group a category** ticked (the default), every playlist group (`group-title`) becomes a category with its channels already in it:

- Categories appear in playlist order. *Uncategorized*, for channels without a group, comes last.
- **New groups** that show up in later playlist refreshes are added automatically.
- If you already have a category with the **same name** as a new group (case doesn't matter), the group joins that category instead of creating a duplicate.
- A category you **delete stays deleted**; it isn't re-created on the next refresh. To get it back, add a new category and add the group to it.

## Organising categories

Each row on the **Categories** tab has:

| Control | What it does |
|---|---|
| ▲ / ▼ | Move the category up or down. This is the folder order. |
| **Name** | Rename the category. |
| **Playlist groups** | The groups whose channels belong to it. Click **×** to remove a group, or use **+ group…** to add one. |
| **Name pattern** | Optional regular expression, case-insensitive. Channels whose name matches are added (see the example below). |
| **Channels** | How many visible channels it has, plus how many you picked by hand. |
| **Show** | Untick to hide the category folder. Its channels stay visible in *All Channels* and any other categories. |
| **Hide channels** | Hide every channel in this category, everywhere. Handy for groups you never watch. |
| **Merge into…** | Move this category's groups, picked channels and name pattern into another category, then remove this one. |
| **Delete** | Remove the category. Its channels stay in *All Channels*. |

For example, the name pattern `sport|espn|dazn` adds every channel with "sport", "espn" or "dazn" anywhere in its name.

Click **+ Category** to make an empty category of your own. Then fill it with groups, a name pattern, or channels picked on the [Channels](channels.md) tab.

Below the table, **Playlist groups not in any category** lists any groups you haven't placed anywhere.

Click **Save** when you're done. The page reloads afterwards, so it shows any categories the server just created.

## How a channel ends up in a category

A channel is in a category if any of these is true:

- its playlist group is listed on the category;
- you added it on the [Channels](channels.md) tab;
- its name matches the category's name pattern.

A channel can be in any number of categories.

## Where categories show up

| Place | What you see |
|---|---|
| IPTV Hub folders | One folder per visible category that has channels, in your order, then *All Channels*. |
| Live TV tuner (`group-title`) | The channel's first visible category, otherwise its playlist group. |
| Live TV channel tags | All of the channel's visible categories. See [Watching](watching.md). |
| [`playlist.m3u` export](exports.md) | Same as the tuner. |

## Turning automatic categories off

Untick **Automatically make each playlist group a category**. Existing categories are kept, but new groups are no longer added. The raw playlist groups can then be shown as extra folders with the setting described in [Settings](settings.md).
