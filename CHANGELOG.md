# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- `SteamStatsDB`, a `SingletonObject` that holds the stats of the game as sub-assets of itself,
  opened from "Window/Steam Toys/Steam Stats DB" and the toolbar dropdown. Its inspector shows
  every stat of the DB and every stat Steam has in one table and marks what is wrong with each:
  no API Name, an API Name used twice in the DB, a stat Steam does not have or has as another
  type, settings that differ from Steam, a stat Steam has and the DB does not, and settings
  that contradict each other, such as a min value above the max value.
  - Create From Steam makes a stat for every stat Steam has and overwrites the settings of the
    ones already there. Stats are updated in place, so references to them survive; a stat Steam
    has as another type is left alone.
  - Add Stat adds an empty stat of any stat class, including classes a game derives from them.
  - The inspector of the selected stat sits under the table, with Ping and Remove From DB.
  - Pull All From Steam, Store Stats and Reset All Stats, with or without achievements.
- `SteamStatsDB.Get`, `TryGet<TStat>`, `Stats` and `PullAllFromSteam`.
- A notice at the top of the stat inspector listing the other stat assets with the same API
  Name. Standalone stat assets stay fully supported.

### Changed

- `SteamStats` is now `SteamStatsDB`. `StoreStats`, `ResetAllStats`, `Initialized`,
  `OnStatsReceived` and `OnStatsStored` stay static, so replacing the class name is all a
  caller needs.
- A stat of the DB takes its API Name as its name once the API Name field is left.

## [0.2.1] - 2026-09-22

### Added

- `BundleElement.HasItem`, false while the element has no item assigned.

### Fixed

- The item inspector draws when its bundle has an element with no item assigned, such as one
  just added to the list. `BundleElementConverter.ToString` threw a `NullReferenceException`
  on it while building the JSON preview, which stopped the whole inspector. Such elements are
  left out of the bundle string.

## [0.2.0] - 2026-09-21

### Added

- `SteamAppCache`, an editor-only reader for the Steam client's `appcache` folder.
  `TryGetStatSchema` returns the stats of an app as configured and published on the partner
  site, from the copy the client downloaded (`appcache/stats/UserGameStatsSchema_<appid>.bin`),
  without a Steam session, a login or network access.
- A Steam section in the stat inspector that compares the asset with the same stat on Steam:
  type, default value, min value, max value, max change, increment only and, for average rate
  stats, window size. Optional settings are compared ticked or not, so a ticked max change of
  0 differs from none on Steam.
  - Pull Stat Settings From Steam copies the settings Steam has into the asset, with undo.
  - Edit on Steam opens the partner site page the stats of the app are edited on.
- `AvgRateStat.WindowSize`, mirroring Window on the partner site. It is in seconds, the unit
  `AddSession` reports session lengths in.
- Current Value and Steam Value at the top of the stat inspector: the value cached in the asset,
  marked while it is still the default, and the one the Steam client holds for the signed-in
  user, read without touching the cache. The Steam value needs a Steam session.
- Value buttons under them: Set Value (Add Session on an average rate stat), Push Value To
  Steam, which also stores the changed stats on the Steam servers, and Pull Value From Steam.
- `SteamStat.IsSynced`, false while the cached value is still the default the stat starts with.

### Changed

- The stat inspector draws its buttons with `InspectorButtonElement` from Toys for Unity.

### Removed

- `IntStat.Sync` and its Sync context menu. Pull Value From Steam in the inspector does the same,
  and so does `TryPullFromSteam` in code.

### Fixed

- `SteamStatEditor` draws for `IntStat`, `FloatStat` and `AvgRateStat`. It was registered for
  the abstract `SteamStat` alone, so none of them used it.

## [0.1.0] - 2026-09-18

### Added

- `SteamSession`, the one place that starts, pumps and stops the Steamworks API, and the
  `SteamManager` singleton that owns it while the game runs.
- `SteamSettings` with the App ID of the project, drawn by `ProjectAppIdAttribute` as a popup
  of the possible App IDs that also writes `steam_appid.txt`.
- The `Window/Steam Toys` menu: Connect To Steam outside play mode, Run Steam and Steam
  Settings, plus a main toolbar dropdown with the same entries.
- Stats: `IntStat`, `FloatStat` and `AvgRateStat` assets that mirror the constraints configured
  on the partner site, and `SteamStats` for storing and resetting them.
- Inventory: assets for Steam Inventory item definitions with their JSON converters, and an
  item editor with a JSON preview.
