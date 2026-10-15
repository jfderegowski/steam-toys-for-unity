# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- `SteamItemsDB`: the item definitions of the game, loaded from `Resources/Steam/Items` (assets
  of their own, not sub-assets), with lookups by itemdefid, the whole file the Steam Inventory
  Service imports (`ToJson`, App ID from the Steam Settings), and warnings about the set:
  missing, repeated or out of range itemdefids, and references to definitions outside the
  folder. "Window/Steam Toys/Steam Items DB" opens it: a table of the definitions, adding one of
  any class with the next free itemdefid, moving one to the trash, and copying or saving the
  JSON. It works on definitions only; owned items are in the Steam Inventory window.
- Create From Steam in the items DB makes a definition for every one the Steam client has for
  the app and the DB lacks, and overwrites the fields Steam reports of the ones it has: name,
  description, display type, icons, colors, trading flags, tags, exchange recipes and tag
  generators, each for the types that use it; the other fields are left as they are. A new
  definition with tags takes the class of the game whose tags type holds a fixed set of
  categories covering all of them, and the class of the package otherwise, whose `Tags` holds
  any category. Tags a class cannot hold, the tags of a bundle, which Steam does not pass on,
  exchange materials with no `TagMaterial<TEnum>` class and references to definitions not in
  the DB are listed in the console.
- `Inventory` reads the Steam inventory of the signed-in player. Each owned item is an
  `InventoryItem`, kept across updates while owned: its `Definition` (an `ItemDef` of type
  `item`), `InstanceId`, `Quantity`, `Flags`, its tags as Steam reported them, generated ones
  included, and merged over the ones of its definition into a tags object of the definition's
  type (`TagsObject`, `GetTags<TTags>`, `HasTag`), and every other property Steam reports for it
  (`Properties`, with `Acquired`, `StateChanged`, `Origin`, `State` and `OriginalInstanceId`),
  since items of one itemdefid differ only in those. Lookups by definition, itemdefid, name, tag
  value, instance ID, definition class or tags type (`Get…`, `TryGet…`, `GetAll…`,
  `GetItems<TTags>`, `GetItemsOfType<TItem>`, `Contains…`, `Count…`), and the calls that change
  it: `Refresh`, `TriggerItemDrop`, `AddPromoItem(s)`, `GrantPromoItems`, `ConsumeItem`,
  `ExchangeItems` and `GenerateItems`, each with an optional completion callback, plus
  `OnChanged`. An itemdefid the items DB lacks gets a definition made at runtime from the one
  published on Steam (name, description, tags, display type, icons, colors, trading flags),
  shared by its items. The flags the client reports as `1` / `0` are read as such.
- "Window/Steam Toys/Steam Inventory" shows the inventory of the signed-in account like the
  inventory page of Steam: a grid of item tiles in their background and name colors, with the
  icon and the quantity, and the clicked item previewed beside it with its large icon, display
  type, description, tags, trading flags, IDs, when and how it was acquired and every property
  Steam reported for it, selecting its definition or consuming one.
  Pages of 25 items as on Steam, with the page size set in the toolbar and remembered. Grant
  Promo Items is in the toolbar. Preview Definitions shows the definitions of the items DB in
  the same layout, without Steam, with Generate For Me to grant one for testing.
- `ItemDef.GenerateItem(quantity)` grants the definition to the signed-in account for
  testing. The inspector of an item, bundle, generator or playtime generator has it as the
  Generate Item button with a quantity field, disabled without a Steam session.
- `JsonPreviewElement` takes a max height past which the JSON scrolls, and whether long lines
  wrap; unwrapped, it scrolls sideways when needed. Both are also `MaxHeight` and `WrapText`, and
  the scrollbars show only while the text does not fit. The defaults keep the old look.
- The ItemDef inspector handles several selected definitions: Generate Item grants each of them,
  the schema warnings of all are listed with the definition name in front, and the JSON preview
  and Copy JSON hold an array of them, Copy JSON + AppID all of them in one import file.
- Inventory: one asset class per ItemDef `type`: `Item`, `Bundle`, `Generator`,
  `PlaytimeGenerator` and `TagGenerator<TEnum>`, the first four usable without code. Steam keeps
  every property on every definition and lets the type decide which ones act, so every schema
  property is a field of `ItemDef`, once, and an abstract `SchemaSupports…` method of its own,
  e.g. `SchemaSupportsPrice()`, which each type implements to say whether it uses it, as the
  schema probes measured: exchange, promo, `granted_manually`, `drop_start_time` and
  `drop_interval` on every type that can be granted, the presentation, Item Store and price
  fields on items and bundles, `bundle` and `tag_generators` on bundles and generators, the item
  fields on items and the drop settings on playtime generators. A class of the game may override
  them. Only those are shown in the inspector (`SchemaSupportsField` maps a field to its method)
  and exported, and one set on a type that does not use it is warned about.
  `ItemDef<TTags>` adds tags: `Item<TTags>`, `Generator<TTags>` and `PlaytimeGenerator<TTags>`
  build on it, and `Item`, `Generator` and `PlaytimeGenerator` are those with `Tags`. A bundle
  has no tags, as Steam does not pass them on to what it creates; a generator copies its tags to
  the item it creates. Types are told apart by `ItemDef.Type`, since `Item<SwordTags>` is not
  an `Item`. A tag generator takes its category and values from the same enum a `Tag<TEnum>`
  uses.
- `TagsDef`, the tags of a definition or an owned item: `category:value` pairs, one value per
  category, kept as strings in a `SerializableDictionary<string, string>` (`GetTags`). An enum
  is a helper only: `GetValue<TEnum>`, `SetValue<TEnum>` and `Has<TEnum>` read the category
  named after it, e.g. `Rarity.Legendary` as `rarity:legendary`. `Tags` holds any category; a
  subclass holds a fixed set by overriding `CanHold`. `SetSteamString` and `MergeSteamString`
  return the pairs they could not hold.
- Inventory: `price_category`, `store_tags`, `store_images`, `purchase_bundle_discount`,
  `container_contents_generator` and `tag_generators`.
- The item inspector warns about settings Steam would reject or ignore, such as a price together
  with a price category, or a drop start time without a manual promo, and about the ones that
  make Steam reject a definition and stop the upload there: an empty name or a bundle without
  contents.
- Schema probes (`Tests/SchemaProbes`): edit mode tests that run against the live Steam inventory
  of a partner account and record, per ItemDef type, what Steam does with granting, exchange,
  promos, `granted_manually`, drops, tags, tag generators, `auto_stack`, `tradable`, `hidden`,
  prices, which properties it keeps for each type, and what reaches the client of definitions that
  break a schema rule. "SteamToys/Inventory/Schema Probes/Save
  JSON" writes the probe definitions to upload; the results go to `Logs/SteamSchemaProbes/report.md`.

### Changed

- `Inventory` is no longer an asset holding definitions: those moved to `SteamItemsDB`. An
  existing Inventory asset loses its script and can be deleted.
- An item definition has no App ID of its own; the JSON takes the one of the Steam Settings.
- `Item` is the ItemDef of type `item`; the shared base is `ItemDef`, and `Inventory.Items`,
  `BundleElement.Item` and the item inspector take any `ItemDef`. The `type` comes from the
  class of the asset, so it is no longer a field.
- The bundle list is `BundleContent` (with `BundleContentConverter`), since `Bundle` is now
  the ItemDef of type `bundle`. In generators its entries are weights.
- Tags are a dictionary of strings (`TagsDef`, `Tags`) instead of a class of `Tag<TEnum>` fields
  per game; `GeneratedTag<TEnum>` is gone, as an owned item reads any tag Steam reports. A tags
  class of the game becomes a `Tags` subclass, its fixed categories in `CanHold`, and the tags
  stored in existing definitions have to be set again, e.g. by Create From Steam.
- A playtime generator has an exchange, which Steam runs like a plain generator's: one pick of
  the content for the materials, apart from the drops.
- `TagMaterial<TTags>` is `TagMaterial<TEnum>`, holding the one tag it matches. It took the
  first pair of a whole tags class, which comes from a `HashSet` in no set order, so a class with
  two tags could put either into the recipe. Subclass it per enum to offer it in recipes.
- `exchange` separates recipes with `;` and the materials of a recipe with `,`, as Steam reads
  it. They were swapped, so any exchange with more than one recipe or material was exported
  wrong.
- A playtime generator exports `drop_limit` of 0 when the drop limit is set, which stops its
  drops, instead of leaving it out.
- The achievement promo rule takes a `SteamAchievement` asset and exports `ach:<API Name>`, as
  Steam reads it. It wrote `ach:<appid>:<name>`.
- The playtime promo rule exports `played:<appid>/<minutes>`; it wrote a `:` before the minutes.
  An incomplete promo rule, such as an achievement rule with no achievement, is left out of the
  promo instead of leaving an empty entry, and the inspector warns about it.
- A price always starts with the `1;` version. It had it only with sales, so a price without
  sales was exported in a format Steam does not read.

### Fixed

- Scrolling over the JSON preview threw a `NullReferenceException` in the rich text parser of
  Unity 6.3's advanced text generator. The preview uses the standard generator, and the Steam
  Properties rows of the inventory window are plain text.

## [0.5.1] - 2026-10-02

### Added

- Create From Steam in the stats DB and the achievements DB lists the stats or achievements of
  the DB that Steam does not have, and offers to remove them, as one undo step. Ones without an
  API Name are left out.

## [0.5.0] - 2026-10-01

### Added

- Achievement icons: `SteamAchievement.Icon`, `LockedIcon` and `CurrentIcon`, sprites imported
  from PNG files in `Assets/Resources/SteamAchievementsIcons`, named after the API Name
  (`<ApiName>.png` and `<ApiName>_Locked.png`). The editor downloads them from the Steam CDN,
  where the schema the Steam client downloaded names them, so a game has them without a Steam
  session.
  - Create From Steam in the achievements DB and Pull Achievement Settings From Steam in the
    achievement inspector download the icons that changed on Steam. A file is written over in
    place, so references to its sprite and its import settings survive a replaced icon.
  - The comparison with Steam has Icon and Locked Icon rows, the achievement inspector shows
    both icons, and the achievements DB table shows the icon of each achievement.
  - The Project window thumbnail of an achievement is its icon, or its locked icon when it has
    only that.
  - Changing the API Name renames the icon files. Removing an achievement from the DB leaves
    them in the folder.
- The rest of the partner site configuration, so a project can define stats and achievements
  in full and send them to Steamworks with the Steam Toys web extension:
  - `SteamStat.DisplayName`, `Permission` (Set By) and `Aggregated`.
  - `SteamAchievement.EnglishDisplayName`, `EnglishDescription` and `Permission`. Named apart
    from `GetDisplayName` and `GetDescription`, which still read the texts in the language of
    the user from Steam.
  - `SteamPermission`, the three Set By options of the partner site.
  - The comparison with Steam and Pull From Steam cover the display names and the achievement
    descriptions, and both DB tables show them. Set By and Aggregated are not compared yet: the
    schema the Steam client downloads showed no trace of them, at least at their defaults.
- A Steamworks JSON preview in the stat and achievement inspectors, for the selected assets, with
  a Copy button, and Copy All As JSON in both DBs. They copy the settings, and for achievements
  the icon files as base64, in the `steam-toys/1` format the Steam Toys web extension pastes into
  the Stats and Achievements pages of Steamworks. The preview names each icon by its size instead
  of showing its base64. Missing or duplicate API Names, settings with problems, and icons the
  partner site would refuse stop the copy with a dialog; the rest are warnings in the Console.

### Changed

- `SteamStat<TValue>.DefaultValue` is a `HasValue<TValue>`, like Min Value and Max Value, so a
  stat can leave Default Value empty the way the partner site does. Unset, the stat starts at 0.
  Defaults saved before read as unset; tick Has Default Value again where one was not 0.
- The Display Name column of the achievements DB shows the name in the asset rather than the
  one on Steam, marked when the two differ, with the description on hover.
- `JsonPreviewElement` logs at most 5000 characters of what it copied, so a copy carrying icons
  does not flood the Console. The clipboard still gets all of it.

## [0.4.0] - 2026-09-28

### Added

- Achievements: `SteamAchievement` assets that mirror Hidden and the progress stat of an
  achievement configured on the partner site, the progress stat being a reference to a stat
  asset.
  - `IsAchieved` and `UnlockTime`, read from Steam once and then served from a local cache,
    with `onAchievedChanged`.
  - `Unlock` and `Clear`, which store the change right away unless told not to; storing is what
    shows the notification.
  - `IndicateProgress`, which shows the progress notification from the progress stat, and
    `IndicateProgressOf(current, max)` for achievements without one.
  - `Progress`, from 0 to 1, worked out from the progress stat.
  - `GetDisplayName` and `GetDescription`, in the language of the user.
  - An unlock triggered by the progress stat reaching its max value reaches the asset as well.
- `SteamAchievementsDB`, a `SingletonObject` that holds the achievements as sub-assets, opened
  from "Window/Steam Toys/Steam Achievements DB" and the toolbar dropdown. Its inspector works
  like the one of the stats DB: a table of every achievement of the DB and of Steam with what
  is wrong with each, Create From Steam, Add Achievement, the inspector of the selected
  achievement, and Pull All From Steam, Store Stats and Clear All Achievements.
- `SteamAchievementsDB.Get`, `TryGet<TAchievement>`, `Achievements`, `PullAllFromSteam`,
  `ClearAllAchievements` and `OnAchievementStored`.
- The achievement inspector: current state, Steam state and progress, Unlock, Clear, Indicate
  Progress and Pull State From Steam, and a comparison with the achievement on Steam, with its
  English display name and description, Pull Achievement Settings From Steam and Edit on
  Steam.
- `SteamAppCache` reads the achievements of the schema as well, into
  `StatSchema.Achievements`.
- `SteamStat.GetValueAsDouble`, the value of a stat of any value type.

## [0.3.0] - 2026-09-22

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
