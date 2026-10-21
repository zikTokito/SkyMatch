SkyMatch 1.2.0 — English Portable Build
========================================

This is a clean English UI build. It does not include user caches, history,
block lists, weather caches, or Workshop previews.

First launch:
1. Scan the local Wallpaper Engine library.
2. Locate by IP and load the current weather.
3. Fetch Workshop candidates for 9 allowed Steam tags.
4. Build vision and preview caches in the background.

Requirements
------------
- Windows 10 / 11, 64-bit
- .NET Framework 4.8 (usually included with Windows 10 2004+)
- Wallpaper Engine
- Network access on first launch

Run
---
Double-click SkyMatch.exe.

User data is stored in:
%LOCALAPPDATA%\SkyMatch

The portable folder contains only the app and models. To reset the app,
exit SkyMatch and delete %LOCALAPPDATA%\SkyMatch.

Notes
-----
- This is an unsigned self-built executable; SmartScreen may warn about an
  unknown publisher.
- Wallpaper Engine subscriptions and wallpaper application still use Steam's
  official flow.
- Source: https://github.com/zikTokito/SkyMatch
- English source branch: https://github.com/zikTokito/SkyMatch/tree/english
- License: GNU AGPL-3.0. Author: Trzipx.

If switching wallpapers stutters, disable "Use high-precision timer" in
Wallpaper Engine: Settings → General → Advanced.
