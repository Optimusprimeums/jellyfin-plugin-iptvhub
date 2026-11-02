---
title: Changelog
nav_order: 13
wiki_title: Changelog
---

# Changelog

## 1.2.1.0

- **Fixed:** Live TV channels are now actually tagged with their categories; 1.2.0.0 matched no channels. The log reports how many were matched after every run.
- **Correction to 1.2.0.0:** it never merged channels or rebuilt Live TV channels, so favourites and recordings were never affected. The `channel-id` attribute it added is removed again.

## 1.2.0.0

- Live TV channels are tagged with their categories (this didn't work until 1.2.1.0).

## 1.1.0.0

- Playlist groups automatically become categories with their channels already in them. New groups are added on later refreshes, and deleted categories stay deleted.
- Categories can be reordered, renamed, merged and hidden, or can hide all their channels.

## 1.0.0.0

- First release: M3U playlists by URL, server path or upload; XMLTV guides; custom categories; per-channel overrides; the IPTV Hub channel; Jellyfin Live TV registration; merged playlist and guide exports.
