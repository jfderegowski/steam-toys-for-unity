# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.10.1] - 2026-11-10

### Fixed

- The treemap no longer rounds percentages to one decimal place: a tile, its tooltip and the chance
  per roll show the share with up to four decimals, so a weight of 7999 of 10000 reads 79.99%, not 80%.

## [0.10.0] - 2026-11-10

### Added

- The contents of a bundle or generator show a treemap under their list, like a stock market map:
  one tile per entry, its area the entry's share. An entry whose item has contents of its own, e.g.
  a generator in a generator, is a group with its name on top and its own entries tiled inside, down
  to the last level. Under a generator a tile shows the chance per roll, multiplied down the levels,
  and under a bundle how many it grants. A tile shows the item's icon, its name in the item's Name
  Color and its Background Color, every tile and group is framed by one grey line, and a click pings
  the item.
- The weights of a generator's entries can be edited in the treemap at any level, in the asset that
  holds them. Drag the edge two tiles share, marked with three dots, to move weight between them.
  Double-click a tile, or choose Change Percentage in its context menu, to type its share among the
  tiles of its level. Equalize Weights on This Level and Equalize Weights Inside give every entry
  the same weight. After a drag the weights are reduced to the smallest numbers with the same odds,
  and Ctrl+Z undoes each change in one step. A bundle's quantities stay in the list.
- The Tag Generator section shows the tag values as a treemap of their odds, editable the same way.
- Container Contents Generator shows the treemap of the chosen generator under the field, and its
  picker opens the search with only the classes of `GeneratorDef<TTags>`.
- A warning when Container Contents Generator is not a generator, as Steam lists drops only of a
  definition of type `generator` in the item's description. Fix clears the field.
- `ContentTreemapElement` draws any serialized list whose elements have a name and a `uint` weight,
  given the relative paths of both. The name is an `ItemDef`, nested with its contents, or anything
  else shown as its text, e.g. a string or an enum.

### Changed

- The bar of a generator's odds under its contents is no longer drawn; the treemap takes its place.
  `WeightBarElement` stays in the package.

## [0.9.0] - 2026-11-09

### Added

- A warning of the `ItemDef` inspector that has a safe fix gets a Fix button, at the top and in the
  frame of its field. A fix only removes what the export leaves out anyway or brings a value into
  its limit, e.g. a contents entry without an item, an incomplete sale or promo rule, a Drop Max
  Per Window over 10 or a Purchase Bundle Discount over 100, and Ctrl+Z undoes it in one step.
  Warnings that need a decision, such as an empty name or a bundle without contents, have none.
- An editor of a subclass of `ItemDef` can add its own warnings: inherit `ItemDefEditor` and
  override the method of a field, e.g. `GetPriceWarnings`, which returns a `Warning` with a message
  and an optional fix. There is one for each serialized field of `ItemDef`, called only when the
  type of the item uses that field, and `GetWarnings` for warnings that belong to no single field.
  The built-in warnings are always shown, and the added ones also appear in the list of the Steam
  Items DB.
- `Warning`, a message with an optional fix, and `ItemDefWarnign`, one about a field of an
  `ItemDef`, whose fix is recorded for undo.

### Changed

- The warnings of the `ItemDef` inspector are listed in the order of their fields, and a warning
  about a field is shown only when the type of the item uses that field.

## [0.8.0] - 2026-11-05

### Added

- `PlaceholderItemDef` takes an itemdefid out of use, as Steam cannot delete a definition: it exports
  only a hidden, `game_only` `item` named `Placeholder`, and the fields of `ItemDef` keep their old
  values. Choose it in the Type field of the inspector to retire a definition.
- A generator shows a bar of the odds under its contents, one segment per entry as wide as its share
  of the total weight. Drag a divider to move weight between the two entries next to it, or with
  Shift between everything on either side, each side keeping its proportions. Double-click a
  segment to type its share in percent, and the reset button gives every entry the same weight.
  After a drag the weights are reduced to the smallest numbers with the same odds, and Ctrl+Z
  undoes it in one step.

### Changed

- `SteamToys` comes first among the packages in the Create menu, under the entries of Unity.
  `SteamToys/Inventory` has a submenu for each type, and `Item` lists `Default Item`, then
  `Placeholder`, then any class added there with the default order.
- The Type field of the `ItemDef` inspector shows the chosen type and class, e.g. `Item/Item`, and
  lists only classes with `CreateAssetMenu`, under the name of their menu entry and in its order.
- The string tags in the tags section are titled `Tags`. Needs toys-for-unity with the label of a
  `PropertyField` passed on to `SerializableDictionaryElement`.
- The contents of a bundle or generator are drawn as just their list, titled after the field that
  holds them, e.g. `Bundle`, without a foldout of their own, like Exchange and Promo.
- Each entry of the contents is one row of standard fields: the item, labelled with its name and
  itemdefid without bold, then its quantity, or its weight in a generator.

## [0.7.0] - 2026-11-04

### Added

- Every `Tags` can hold string tags next to its own, added in the inspector or imported from Steam.
  The tags drawer shows an error, also while its section is folded, for a string tag named like a
  tag the class already has.
- `TagsConverter.FromString` takes `addMissing`, which adds the categories the tags do not hold yet
  as string tags.
- `PromoRuleConverter` is a JSON converter, with one for each rule: `ManualPromoRuleConverter`,
  `OwnsPromoRuleConverter`, `AchievementPromoRuleConverter` and `PlayedPromoRuleConverter`.
- `NamedSelectTypeElement`, a `SelectTypeElement` of toys-for-unity named after its value, e.g.
  `Legendary x3`. Needs toys-for-unity after 0.2.1, with `createContent` and `Label` on
  `SelectTypeElement`.

### Changed

- **Breaking:** `InventoryItem<TItemDef>` is `InventoryItem<TItemDef, TTags>`, which types `Tags`
  and creates them. Scripts generated for a kind of item use it, so generate them again.
- **Breaking:** a subclass of `Tags` overrides `GetTagNames`, `TryGetTagValue` and `TrySetTagValue`
  instead of `Keys`, `TryGetValue` and `TrySetValue`. `TrySetValue` only changes a tag that exists.
- **Breaking:** `DefaultTags` keeps its tags as the string tags of `Tags`, so what it saved in its
  own dictionary is no longer read. Bring it back with Create From Steam or add it again.
- **Breaking:** `PromoRule` is abstract, and `GetValue` with the converters replaces `ToSteamString`.
- **Breaking:** `ItemConverter` is now `ItemDefConverter`.
- Exchange draws each recipe as its list of materials, without a foldout of its own, named after
  what it takes, e.g. `Atom x1 + Legendary x3`. Promo draws its rules as a list the same way.
- Materials and promo rules are a `NamedSelectTypeElement`, with the type dropdown in the header of
  a foldout, lined up with the other fields. Under it, an item or a tag gets one row of standard
  fields with the quantity, and a rule gets its fields. `MaterialDrawer` draws items and tags, in
  place of `ItemMaterialDrawer` and `TagMaterialDrawer`, which are removed.
- `ToString` of a tag material and of a promo rule is shorter: `Legendary x3`, `App 480`,
  `App 480, 120 min`, `Win` and `Manual`.

### Fixed

- Create From Steam writes the tags Steam reports to the definition, and to a `DefaultTags` adds the
  categories it does not hold yet. It also picks a class of the game by its tags with any tags
  class, not only one with a fixed set.
- A definition made at runtime from the one published on Steam keeps its tags.

## [0.6.0] - 2026-10-22

### Added

- `SteamItemsDB`: the item definitions of the game, loaded from `Resources/Steam/Items` (assets
  of their own, not sub-assets), with lookups by itemdefid, the whole file the Steam Inventory
  Service imports (`ToJson`, App ID from the Steam Settings), and warnings about the set:
  missing, repeated or out of range itemdefids, and references to definitions outside the
  folder. "Window/Steam Toys/Steam Items DB" opens it: a table of the definitions, adding one of
  any class with the next free itemdefid, moving one to the trash, and copying or saving the
  JSON. It works on definitions only; owned items are in the Steam Inventory window.
- The items DB table also lists the definitions the Steam client reports for the app, marks what
  Steam would reject in each definition and where one differs from the definition on Steam, and
  shows the inspector of the selected one under it. The last definitions read stay for the
  comparison after the Steam session ends. The client never lists bundles, tag generators and
  playtime generators, so those are looked up by itemdefid.
- Create From Steam in the items DB makes a definition for every one the Steam client has for
  the app and the DB lacks, and overwrites the fields Steam reports of the ones it has: name,
  description, display type, icons, colors, trading flags, tags, exchange recipes, tag
  generators and the values of a tag generator, each for the types that use it; the other fields
  are left as they are. It first offers to add to the Steam Item Definitions the tag categories
  and values Steam uses and a kind of item for each set of tags none takes, and to generate their
  scripts. A window then lists the new definitions in groups of the same type and tag categories,
  each with the class to make them with: a class of the game whose tags hold those categories,
  or a Default class of the package. Tags a class cannot hold, the tags of a bundle, which Steam
  does not pass on, exchange materials with no `TagMaterial<TEnum>` class and references to
  definitions not in the DB are listed in the console.
- "Window/Steam Toys/Steam Item Definitions" generates the scripts of the game's items from
  tags and kinds of items, kept in `ProjectSettings/SteamItemDefinitions.asset`, out of builds.
  A tag generates its enum, whose values keep the number they were given so renaming, reordering
  or removing values leaves saved assets as they were, and optionally its `TagGeneratorDef<TEnum>`
  and `TagMaterial<TEnum>` classes. A kind of item, e.g. `Event`, takes tags and generates its
  tags classes, with each tag a property of its enum that is null while not set, and the partial
  `ItemDef`, `InventoryItem`, `GeneratorDef` and `PlaytimeGeneratorDef` classes chosen. The window
  lists what keeps one from generating and the scripts it writes; Generate All writes the tags
  first. A renamed or moved script is moved, not written anew, and scripts no longer generated
  can be deleted or kept as the game's own.
- `Inventory` reads the Steam inventory of the signed-in player. Each owned item is an
  `InventoryItem`, kept across updates while owned: its `Definition` (an `ItemDef` of type
  `item`), `InstanceId`, `Quantity`, `Flags`, the tags Steam reports on the item, which its tag
  generators and the generator that made it set (`Tags`, `TryGetTag`, `HasTag`; the tags of its
  definition stay on `Definition`), and every other property Steam reports for it
  (`Properties`, with `Acquired`, `StateChanged`, `Origin`, `State` and `OriginalInstanceId`),
  since items of one itemdefid differ only in those. `ItemDef.CreateInventoryItem` makes it, so
  a class of the game can have its own, e.g. an `InventoryItem<TItemDef, TTags>` with typed tags.
  Lookups by definition, itemdefid, name, tag value, instance ID, definition class or item class
  (`Get…`, `TryGet…`, `GetAll…`, `GetItems<TItem>`, `GetItemsOfType<TItemDef>`, `Contains…`,
  `Count…`), and the calls that change it: `Refresh`, `TriggerItemDrop`, `AddPromoItem(s)`,
  `GrantPromoItems`, `ConsumeItem`, `ExchangeItems` and `GenerateItems`, each with an optional
  completion callback, plus `OnChanged` and `OnDefinitionsChanged`. An itemdefid the items DB
  lacks gets a definition made at runtime from the one published on Steam (name, description,
  tags, display type, icons, colors, trading flags), shared by its items. The flags the client
  reports as `1` / `0` are read as such.
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
- Inventory: one asset class per ItemDef `type`: `ItemDef<TTags>`, `BundleDef`,
  `GeneratorDef<TTags>`, `PlaytimeGeneratorDef<TTags>` and `TagGeneratorDef<TEnum>`, with
  `DefaultItemDef`, `DefaultBundleDef`, `DefaultGeneratorDef`, `DefaultPlaytimeGeneratorDef` and
  `DefaultTagGeneratorDef` usable without code. Steam keeps
  every property on every definition and lets the type decide which ones act, so every schema
  property is a field of `ItemDef`, once, and an abstract `SchemaSupports…` method of its own,
  e.g. `SchemaSupportsPrice()`, which each type implements to say whether it uses it, as the
  schema probes measured: exchange, promo, `granted_manually`, `drop_start_time` and
  `drop_interval` on every type that can be granted, the presentation, Item Store and price
  fields on items and bundles, `bundle` and `tag_generators` on bundles and generators, the item
  fields on items and the drop settings on playtime generators. A class of the game may override
  them. Only those are shown in the inspector (`SchemaSupportsField` maps a field to its method)
  and exported, and one set on a type that does not use it is warned about.
  `ItemDef<TTags>` is the type `item` with tags, and `GeneratorDef<TTags>` and
  `PlaytimeGeneratorDef<TTags>` build on it; the Default classes take `DefaultTags`. A bundle
  has no tags, as Steam does not pass them on to what it creates; a generator copies its tags to
  the item it creates. Types are told apart by `ItemDef.Type`. A tag generator takes its
  category and values from an enum (`TagGeneratorDef<TEnum>`), or as Steam has them
  (`DefaultTagGeneratorDef`).
- `Tags`, the tags of a definition or an owned item: `category:value` pairs, one value per
  category, kept as strings, read as an `IReadOnlyDictionary<string, string>` and set with
  `TrySetValue`. An enum is a helper only: `Contains<TEnum>`, `GetTagName<TEnum>`,
  `GetTagValue` and `TryParseTagValue` name the category after it, e.g. `Rarity.Legendary` as
  `rarity:legendary`. `DefaultTags` holds any category in a `SerializableDictionary<string,
  string>`; a subclass holds a fixed set, as the generated ones do. `TagsConverter` writes them.
- Inventory: `price_category`, `store_tags`, `store_images`, `purchase_bundle_discount`,
  `container_contents_generator` and `tag_generators`.
- The item inspector warns about settings Steam would reject or ignore, such as a price together
  with a price category, or a drop start time without a manual promo, and about the ones that
  make Steam reject a definition and stop the upload there: an empty name or a bundle without
  contents.
- The ItemDef inspector groups the fields in sections with icons that collapse and expand,
  animated, and remember it; a field that needs fixing is framed with its warnings. A dropdown
  at the top changes the class of the definition among every class of its Steam type and the
  others, keeping the values both classes have and warning which tags may not carry over.
- Drawers: `SteamDateTime` as one row of date and time in UTC with buttons to set it to now or
  clear it, warning while the text is not a date; `PriceValue` with about how many dollars it
  is (`PriceValue.GetUsdAmount`); `Promotion`, `Promo` and `Recipe` as foldouts whose header is
  what they hold, e.g. `owns app 480, manual`, after the field name or, in a list, alone; tags
  and `Exchange` as sections; a `TagMaterial<TEnum>` as one row of the tag, its value and the
  quantity. The buttons of a `Url` sit evenly at the right edge with icons of their own, and
  Paste is enabled only while the clipboard holds an `http://` or `https://` URL, which it
  pastes trimmed.
- `ToString` of `Promotion`, `PriceValue`, `SteamDateTime`, `Promo` and its rules, `Recipe`
  and its materials reads as the inspector shows them, e.g. `2.99 PLN ≈ 0.81 $` or
  `Atom x1 + Item Rarity: Common x3`.
- The Steam dropdown of the main toolbar is colored by the Steam status, as the friends list
  does: plain while the client is closed, blue while it runs and green once this editor or its
  game is connected.
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
- `Item` is gone: the ItemDef of type `item` is `ItemDef<TTags>`, `DefaultItemDef` with tags of
  any category; the shared base is `ItemDef`, and `Inventory.Items`, `BundleElement.Item` and
  the item inspector take any `ItemDef`. The `type` comes from the class of the asset, so it is
  no longer a field.
- The bundle list is `BundleContent` (with `BundleContentConverter`), since `BundleDef` is the
  ItemDef of type `bundle`. In generators its entries are weights.
- Tags are strings (`Tags`) instead of a class of `Tag<TEnum>` fields per game;
  `GeneratedTag<TEnum>` is gone, as an owned item reads any tag Steam reports. A tags class of
  the game becomes a `Tags` subclass, best generated by the Steam Item Definitions, and the tags
  stored in existing definitions have to be set again, e.g. by Create From Steam.
- A playtime generator has an exchange, which Steam runs like a plain generator's: one pick of
  the content for the materials, apart from the drops.
- `TagMaterial<TTags>` is `TagMaterial<TEnum>`, holding the one tag it matches. It took the
  first pair of a whole tags class, which comes from a `HashSet` in no set order, so a class with
  two tags could put either into the recipe. Subclass it per enum to offer it in recipes; the
  Steam Item Definitions generate one for each tag.
- `Material` is abstract with `GetTagName`, `GetTagValue` and `GetQuantity`, and
  `MaterialConverter` writes it: an item when it has no tag name, e.g. `100x1`, a tag otherwise,
  e.g. `rarity:legendary*3`. The Steam strings of prices, promotions, exchanges, recipes and
  materials are written by their converters (`PriceConverter.ToString`,
  `ExchangeConverter.ToString`, …); `ToSteamString` of `Price`, `PriceValue`, `Promotion`,
  `Exchange`, `Recipe` and `Material` is gone.
- A Steam client that is not running is no longer logged as an error when the session starts.
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
