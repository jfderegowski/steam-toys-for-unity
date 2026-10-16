using System;
using System.Collections.Generic;
using System.Linq;
using SteamToys.Runtime.InventorySystem;
using SteamToys.Runtime.InventorySystem.Converters;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

namespace SteamToys.Editor.InventorySystem
{
    /// <summary>
    /// The Steam inventory of the signed-in account, laid out like the inventory page of Steam: a grid of item tiles
    /// in their background and name colors, and the selected item previewed beside it with its large icon,
    /// description and tags.
    /// <para>
    /// Without a Steam session there is nothing to read, so Preview Definitions shows the definitions of
    /// <see cref="SteamItemsDB"/> in the same layout instead, to check how they will look.
    /// </para>
    /// </summary>
    public class SteamInventoryWindow : EditorWindow
    {
        internal const string MenuPath = "Window/Steam Toys/Steam Inventory";

        private const float TileSize = 92;
        private const float TileMargin = 3;
        private const float DetailWidth = 300;
        private const float DetailIconSize = 200;

        private const int DefaultItemsPerPage = 25;
        private const int MaxItemsPerPage = 500;
        private const string ItemsPerPagePref = "SteamToys.SteamInventoryWindow.ItemsPerPage";

        private static readonly Color WindowBackground = new Color32(0x1B, 0x28, 0x38, 0xFF);
        private static readonly Color GridBackground = new Color32(0x16, 0x20, 0x2D, 0xFF);
        private static readonly Color TileBackground = new Color32(0x3D, 0x44, 0x50, 0xFF);
        private static readonly Color TileBorder = new Color32(0x5A, 0x63, 0x6F, 0xFF);
        private static readonly Color SelectedBorder = new Color32(0x66, 0xC0, 0xF4, 0xFF);
        private static readonly Color TextColor = new Color32(0xC6, 0xD4, 0xDF, 0xFF);
        private static readonly Color DimTextColor = new Color32(0x8F, 0x98, 0xA0, 0xFF);
        private static readonly Color TagColor = new Color32(0x9D, 0xC9, 0xE6, 0xFF);

        private readonly Dictionary<string, Texture2D> _icons = new();
        private readonly Dictionary<string, List<Action<Texture2D>>> _loadingIcons = new();
        private readonly Dictionary<Entry, VisualElement> _tiles = new();

        private readonly List<Entry> _shownItems = new();

        private Entry _selected;
        private int _page;

        private ToolbarToggle _previewDefinitions;
        private ToolbarSearchField _search;
        private ToolbarButton _refreshButton;
        private ToolbarButton _grantPromoButton;
        private IntegerField _itemsPerPage;
        private Button _previousPage;
        private Button _nextPage;
        private Label _pageLabel;
        private Label _status;
        private VisualElement _grid;
        private VisualElement _detail;

        [MenuItem(MenuPath)]
        public static void Open()
        {
            var window = GetWindow<SteamInventoryWindow>();

            window.titleContent = new GUIContent("Steam Inventory");
            window.minSize = new Vector2(560, 360);
        }

        #region Lifecycle

        private void OnEnable() => Inventory.OnChanged += Rebuild;

        private void OnDisable() => Inventory.OnChanged -= Rebuild;

        private void OnDestroy()
        {
            foreach (var icon in _icons.Values)
            {
                if (icon)
                    DestroyImmediate(icon);
            }

            _icons.Clear();
        }

        public void CreateGUI()
        {
            rootVisualElement.style.backgroundColor = WindowBackground;

            rootVisualElement.Add(CreateToolbar());

            _status = new Label { style = { color = DimTextColor, marginLeft = 6, marginTop = 4, marginBottom = 4 } };
            rootVisualElement.Add(_status);

            var body = new VisualElement { style = { flexDirection = FlexDirection.Row, flexGrow = 1 } };

            var gridScroll = new ScrollView(ScrollViewMode.Vertical)
            {
                style = { flexGrow = 1, backgroundColor = GridBackground, marginLeft = 6, marginBottom = 6 }
            };

            _grid = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap, paddingLeft = 4, paddingTop = 4 }
            };

            gridScroll.Add(_grid);

            var gridColumn = new VisualElement { style = { flexGrow = 1 } };

            gridColumn.Add(gridScroll);
            gridColumn.Add(CreatePager());
            body.Add(gridColumn);

            _detail = new ScrollView(ScrollViewMode.Vertical)
            {
                style = { width = DetailWidth, flexShrink = 0, marginLeft = 6, marginRight = 6, marginBottom = 6 }
            };

            body.Add(_detail);
            rootVisualElement.Add(body);

            Rebuild();

            // Polled, because nothing tells the window when the Steam session starts or stops.
            rootVisualElement.schedule.Execute(UpdateStatus).Every(1000);
        }

        private VisualElement CreateToolbar()
        {
            var toolbar = new Toolbar();

            _refreshButton = new ToolbarButton(() => Inventory.Refresh()) { text = "Refresh", tooltip = "Reads every item the signed-in account owns from Steam." };
            toolbar.Add(_refreshButton);

            _grantPromoButton = new ToolbarButton(() => Inventory.GrantPromoItems())
            {
                text = "Grant Promo Items",
                tooltip = "Grants the signed-in account every promo item it meets the rules of, except manual ones."
            };

            toolbar.Add(_grantPromoButton);

            _previewDefinitions = new ToolbarToggle
            {
                text = "Preview Definitions",
                tooltip = "Shows the definitions of the Steam Items DB instead of the owned items, to check how they look."
            };

            _previewDefinitions.RegisterValueChangedCallback(_ =>
            {
                _page = 0;
                _selected = null;

                Rebuild();
            });
            toolbar.Add(_previewDefinitions);

            toolbar.Add(new ToolbarSpacer { flex = true });

            _itemsPerPage = new IntegerField("Per Page")
            {
                value = Mathf.Clamp(EditorPrefs.GetInt(ItemsPerPagePref, DefaultItemsPerPage), 1, MaxItemsPerPage),
                isDelayed = true,
                tooltip = $"How many items one page shows; Steam shows {DefaultItemsPerPage}.",
                style = { width = 110 }
            };

            _itemsPerPage.labelElement.style.minWidth = 0;
            _itemsPerPage.labelElement.style.marginRight = 4;

            _itemsPerPage.RegisterValueChangedCallback(evt =>
            {
                var value = Mathf.Clamp(evt.newValue, 1, MaxItemsPerPage);

                _itemsPerPage.SetValueWithoutNotify(value);
                EditorPrefs.SetInt(ItemsPerPagePref, value);

                ShowPageOf(_selected);
            });

            toolbar.Add(_itemsPerPage);

            _search = new ToolbarSearchField { style = { width = 200 } };
            _search.RegisterValueChangedCallback(_ =>
            {
                _page = 0;

                Rebuild();
            });
            toolbar.Add(_search);

            return toolbar;
        }

        /// <summary>The page arrows under the grid, as on the inventory page of Steam.</summary>
        private VisualElement CreatePager()
        {
            var pager = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.FlexEnd,
                    alignItems = Align.Center,
                    marginLeft = 6,
                    marginBottom = 6
                }
            };

            _previousPage = new Button(() => SetPage(_page - 1)) { text = "<", style = { width = 28 } };
            _nextPage = new Button(() => SetPage(_page + 1)) { text = ">", style = { width = 28 } };

            _pageLabel = new Label
            {
                style = { color = TextColor, marginLeft = 6, marginRight = 6, unityTextAlign = TextAnchor.MiddleCenter }
            };

            pager.Add(_previousPage);
            pager.Add(_pageLabel);
            pager.Add(_nextPage);

            return pager;
        }

        #endregion

        #region Grid

        private IEnumerable<Entry> GetShownItems()
        {
            var entries = _previewDefinitions.value
                // Only the types players see: items and bundles.
                ? SteamItemsDB.Instance.GetAll<ItemDef>().Where(definition => definition && definition.SchemaSupportsIconUrl())
                    .Select(definition => new Entry(definition, null))
                : Inventory.Items.Where(item => item.Definition).Select(item => new Entry(item.Definition, item));

            var search = _search.value?.Trim() ?? string.Empty;

            return search.Length == 0 ? entries : entries.Where(entry => Matches(entry, search));
        }

        private static bool Matches(Entry entry, string search) =>
            (entry.Definition.Name?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
            entry.Definition.name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
            (entry.Definition.DisplayType?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
            entry.Definition.ItemDefId.ToString() == search ||
            entry.Tags.Contains(search, StringComparison.OrdinalIgnoreCase);

        private int ItemsPerPage => _itemsPerPage?.value ?? DefaultItemsPerPage;

        private int PageCount => Mathf.Max(1, (_shownItems.Count + ItemsPerPage - 1) / ItemsPerPage);

        /// <summary>Gathers the items to show again, keeping the page and the selection where possible.</summary>
        private void Rebuild()
        {
            if (_grid == null)
                return;

            var selectedKey = _selected?.Key;

            _shownItems.Clear();
            _shownItems.AddRange(GetShownItems());

            // Entries are made again on every rebuild, so the selection is found again by what it shows.
            _selected = _shownItems.FirstOrDefault(entry => Equals(entry.Key, selectedKey));

            if (_selected == null && _shownItems.Count > 0)
                _selected = _shownItems[Mathf.Min(_page * ItemsPerPage, _shownItems.Count - 1)];

            ShowDetail(_selected);
            SetPage(_page);
        }

        /// <summary>Turns to the page that holds <paramref name="item"/>, e.g. after the page size changed.</summary>
        private void ShowPageOf(Entry entry)
        {
            var index = entry != null ? _shownItems.IndexOf(entry) : -1;

            SetPage(index < 0 ? 0 : index / ItemsPerPage);
        }

        private void SetPage(int page)
        {
            if (_grid == null)
                return;

            _page = Mathf.Clamp(page, 0, PageCount - 1);

            _grid.Clear();
            _tiles.Clear();

            foreach (var entry in _shownItems.Skip(_page * ItemsPerPage).Take(ItemsPerPage))
            {
                var tile = CreateTile(entry);

                _tiles[entry] = tile;
                _grid.Add(tile);
            }

            _pageLabel.text = $"{_page + 1} of {PageCount}";
            _previousPage.SetEnabled(_page > 0);
            _nextPage.SetEnabled(_page < PageCount - 1);

            UpdateSelection();
            UpdateStatus();
        }

        private VisualElement CreateTile(Entry entry)
        {
            var item = entry.Definition;

            var tile = new VisualElement
            {
                tooltip = GetDisplayName(item),
                style =
                {
                    width = TileSize,
                    height = TileSize,
                    marginLeft = TileMargin,
                    marginRight = TileMargin,
                    marginTop = TileMargin,
                    marginBottom = TileMargin,
                    backgroundColor = HasColor(item.BackgroundColor) ? Opaque(item.BackgroundColor) : TileBackground,
                    justifyContent = Justify.Center,
                    alignItems = Align.Center
                }
            };

            SetBorder(tile, GetBorderColor(item), 1);

            var icon = new Image { scaleMode = ScaleMode.ScaleToFit, style = { width = TileSize - 12, height = TileSize - 12 } };
            tile.Add(icon);

            LoadIcon(item.IconUrl, texture => icon.image = texture, () => icon.Add(CreatePlaceholder(item, 11)));

            var quantity = entry.Quantity;

            if (quantity > 1)
            {
                tile.Add(new Label(quantity.ToString())
                {
                    style =
                    {
                        position = Position.Absolute,
                        right = 3,
                        bottom = 1,
                        color = TextColor,
                        fontSize = 11,
                        unityFontStyleAndWeight = FontStyle.Bold
                    }
                });
            }

            tile.RegisterCallback<ClickEvent>(_ =>
            {
                _selected = entry;

                UpdateSelection();
                ShowDetail(entry);
            });

            return tile;
        }

        private void UpdateSelection()
        {
            foreach (var (entry, tile) in _tiles)
            {
                var selected = entry == _selected;

                SetBorder(tile, selected ? SelectedBorder : GetBorderColor(entry.Definition), selected ? 2 : 1);
            }
        }

        private void UpdateStatus()
        {
            if (_status == null)
                return;

            _refreshButton.SetEnabled(Inventory.Initialized);
            _grantPromoButton.SetEnabled(Inventory.Initialized);

            _status.text = _previewDefinitions.value
                ? $"Previewing {_shownItems.Count} definitions of {SteamItemsDB.AssetsFolder}."
                : !Inventory.Initialized
                    ? "No Steam session: start play mode or Window/Steam Toys/Connect To Steam, or use Preview Definitions."
                    : Inventory.IsReady
                        ? $"{Inventory.Items.Count} items owned by the signed-in account."
                        : "Not read yet: press Refresh.";
        }

        #endregion

        #region Detail

        private void ShowDetail(Entry entry)
        {
            _detail.Clear();

            if (entry == null)
                return;

            var item = entry.Definition;
            var owned = entry.Owned;
            var isOwned = owned != null;

            var iconFrame = new VisualElement
            {
                style =
                {
                    height = DetailIconSize + 16,
                    backgroundColor = HasColor(item.BackgroundColor) ? Opaque(item.BackgroundColor) : TileBackground,
                    justifyContent = Justify.Center,
                    alignItems = Align.Center,
                    marginBottom = 8
                }
            };

            var icon = new Image { scaleMode = ScaleMode.ScaleToFit, style = { width = DetailIconSize, height = DetailIconSize } };
            iconFrame.Add(icon);
            _detail.Add(iconFrame);

            var largeIcon = item.IconUrlLarge is { HasValue: true } ? item.IconUrlLarge : item.IconUrl;
            LoadIcon(largeIcon, texture => icon.image = texture, () => icon.Add(CreatePlaceholder(item, 22)));

            _detail.Add(new Label(GetDisplayName(item))
            {
                style =
                {
                    fontSize = 16,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = HasColor(item.NameColor) ? Opaque(item.NameColor) : TextColor,
                    whiteSpace = WhiteSpace.Normal
                }
            });

            if (!string.IsNullOrEmpty(item.DisplayType))
                _detail.Add(CreateText(item.DisplayType, DimTextColor, 0, 4));

            if (!string.IsNullOrEmpty(item.Description))
                _detail.Add(CreateText(item.Description, TextColor, 6, 6));

            var tags = entry.Tags;

            if (tags.Length > 0)
            {
                _detail.Add(CreateText("Tags", DimTextColor, 6, 0));

                foreach (var tag in tags.Split(';'))
                {
                    var separator = tag.IndexOf(':');

                    _detail.Add(CreateText(separator < 0
                        ? tag
                        : $"{Humanize(tag[..separator])}: {Humanize(tag[(separator + 1)..])}",
                        TagColor, 0, 0));
                }
            }

            _detail.Add(CreateText(DescribeTrading(item, owned), DimTextColor, 8, 0));

            if (isOwned)
            {
                _detail.Add(CreateText($"Quantity: {owned.Quantity}", DimTextColor, 4, 0));
                _detail.Add(CreateText($"Instance ID: {owned.InstanceId.m_SteamItemInstanceID}", DimTextColor, 0, 0));

                if (owned.Acquired.HasValue)
                    _detail.Add(CreateText($"Acquired: {owned.Acquired.UtcDateTime.ToLocalTime():g}", DimTextColor, 0, 0));

                if (!string.IsNullOrEmpty(owned.Origin))
                    _detail.Add(CreateText($"Origin: {owned.Origin}", DimTextColor, 0, 0));
            }

            _detail.Add(CreateText($"Itemdefid: {item.ItemDefId} ({item.Type})", DimTextColor, isOwned ? 0 : 4, 8));

            var buttons = new VisualElement { style = { flexDirection = FlexDirection.Row } };

            // A definition made from the one published on Steam lives only in memory, so there is no asset to select.
            var isAsset = EditorUtility.IsPersistent(item);

            var ping = new Button(() =>
            {
                Selection.activeObject = item;
                EditorGUIUtility.PingObject(item);
            })
            {
                text = "Select Definition",
                tooltip = isAsset
                    ? "Selects the definition asset this item comes from."
                    : "The Steam Items DB has no definition for this itemdefid; it is shown from the one published on Steam.",
                style = { flexGrow = 1 }
            };

            ping.SetEnabled(isAsset);
            buttons.Add(ping);

            if (!isOwned)
            {
                var generate = new Button(() => Inventory.GenerateItems(new[] { ((ItemDef)item, 1u) }))
                {
                    text = "Generate For Me",
                    tooltip = "Grants one to the signed-in account, for testing. Works only for accounts of the " +
                              "Steamworks partner group, and only once the definition is published on Steam.",
                    style = { flexGrow = 1 }
                };

                generate.SetEnabled(Inventory.Initialized);
                buttons.Add(generate);
            }

            if (isOwned)
            {
                buttons.Add(new Button(() => ConsumeOne(owned))
                {
                    text = "Consume 1",
                    tooltip = "Removes one of this item from the signed-in account for good.",
                    style = { flexGrow = 1 }
                });
            }

            _detail.Add(buttons);

            if (isOwned && owned.Properties.Count > 0)
                _detail.Add(CreatePropertiesFoldout(owned));
        }

        /// <summary>Every property Steam reported for the owned item, as it sent them.</summary>
        private static Foldout CreatePropertiesFoldout(InventoryItem item)
        {
            var foldout = new Foldout { text = "Steam Properties", value = false, style = { marginTop = 8 } };

            foldout.Q<Toggle>().style.color = DimTextColor;

            foreach (var (name, value) in item.Properties.OrderBy(property => property.Key, StringComparer.OrdinalIgnoreCase))
            {
                var row = CreateText($"{name}: {(string.IsNullOrEmpty(value) ? "-" : value)}", DimTextColor, 0, 0);

                // Shown as Steam sent it: plain text, so no value is read as a rich text tag.
                row.enableRichText = false;
                row.selection.isSelectable = true;
                foldout.Add(row);
            }

            return foldout;
        }

        private static void ConsumeOne(InventoryItem item)
        {
            var name = GetDisplayName(item.Definition);

            if (!EditorUtility.DisplayDialog("Consume Item",
                    $"Remove one {name} from the signed-in account on Steam?\n\nThis cannot be undone.",
                    "Consume", "Cancel"))
                return;

            Inventory.ConsumeItem(item, 1, succeeded =>
            {
                if (!succeeded)
                    Debug.LogWarning($"Steam did not consume {name}.");
            });
        }

        private static string DescribeTrading(ItemDef item, InventoryItem owned)
        {
            if (item.Type != ItemType.Item)
                return item.Type == ItemType.Bundle ? "Bundle: expands into its contents when granted." : string.Empty;

            var tradable = item.Tradable && !(owned?.IsNoTrade ?? false);

            return (tradable ? "Tradable" : "Not tradable") + ", " + (item.Marketable ? "Marketable" : "Not marketable");
        }

        private static Label CreateText(string text, Color color, float marginTop, float marginBottom) => new(text)
        {
            style =
            {
                color = color,
                whiteSpace = WhiteSpace.Normal,
                marginTop = marginTop,
                marginBottom = marginBottom
            }
        };

        #endregion

        #region Icons

        /// <summary>
        /// Shows the image at <paramref name="url"/>, downloaded once per window; <paramref name="onMissing"/> runs
        /// instead when there is no URL or the download fails.
        /// </summary>
        private void LoadIcon(Url url, Action<Texture2D> onLoaded, Action onMissing)
        {
            if (url is not { HasValue: true })
            {
                onMissing();
                return;
            }

            var key = url.Value;

            if (_icons.TryGetValue(key, out var cached))
            {
                if (cached)
                    onLoaded(cached);
                else
                    onMissing();

                return;
            }

            void OnDone(Texture2D texture)
            {
                if (texture)
                    onLoaded(texture);
                else
                    onMissing();
            }

            if (_loadingIcons.TryGetValue(key, out var waiting))
            {
                waiting.Add(OnDone);
                return;
            }

            _loadingIcons[key] = new List<Action<Texture2D>> { OnDone };

            var request = UnityWebRequestTexture.GetTexture(key);

            request.SendWebRequest().completed += _ =>
            {
                Texture2D texture = null;

                if (request.result == UnityWebRequest.Result.Success)
                {
                    texture = DownloadHandlerTexture.GetContent(request);
                    texture.hideFlags = HideFlags.DontSave;
                }
                else
                {
                    Debug.LogWarning($"Could not download the item icon {key}: {request.error}");
                }

                request.Dispose();

                // Remembered even when it failed, so a broken URL is not downloaded again on every rebuild.
                _icons[key] = texture;

                if (_loadingIcons.Remove(key, out var callbacks))
                {
                    foreach (var callback in callbacks)
                        callback(texture);
                }
            };
        }

        /// <summary>The first letter of the name, for an item without an icon.</summary>
        private static Label CreatePlaceholder(ItemDef item, int fontSize)
        {
            var name = GetDisplayName(item);

            return new Label(name.Length > 0 ? name[..1].ToUpperInvariant() : "?")
            {
                style =
                {
                    flexGrow = 1,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    fontSize = fontSize * 3,
                    color = HasColor(item.NameColor) ? Opaque(item.NameColor) : DimTextColor,
                    unityFontStyleAndWeight = FontStyle.Bold
                }
            };
        }

        #endregion

        #region Helpers

        /// <summary>A Steam tag token as words, e.g. "battle_scarred" as "Battle Scarred".</summary>
        private static string Humanize(string token) =>
            string.Join(" ", token.Trim().Split('_', StringSplitOptions.RemoveEmptyEntries)
                .Select(word => char.ToUpperInvariant(word[0]) + word[1..]));

        private static string GetDisplayName(ItemDef item) =>
            string.IsNullOrEmpty(item.Name) ? item.name : item.Name;

        private static Color GetBorderColor(ItemDef item) =>
            HasColor(item.NameColor) ? Opaque(item.NameColor) : TileBorder;

        // A color left at its default (transparent black) counts as unset, as in the exported JSON.
        private static bool HasColor(Color32 color) => color.r != 0 || color.g != 0 || color.b != 0 || color.a != 0;

        private static Color Opaque(Color32 color) => new Color32(color.r, color.g, color.b, 0xFF);

        private static void SetBorder(VisualElement element, Color color, float width)
        {
            element.style.borderLeftColor = color;
            element.style.borderRightColor = color;
            element.style.borderTopColor = color;
            element.style.borderBottomColor = color;
            element.style.borderLeftWidth = width;
            element.style.borderRightWidth = width;
            element.style.borderTopWidth = width;
            element.style.borderBottomWidth = width;
        }

        #endregion

        /// <summary>One tile: a definition, and the owned item when the grid shows the inventory.</summary>
        private sealed class Entry
        {
            public readonly ItemDef Definition;
            public readonly InventoryItem Owned;

            public Entry(ItemDef definition, InventoryItem owned)
            {
                Definition = definition;
                Owned = owned;
            }

            /// <summary>What the tile shows, which stays the same across rebuilds: the owned item, or the definition.</summary>
            public object Key => (object)Owned ?? Definition;

            public uint Quantity => Owned?.Quantity ?? 0;

            /// <summary>The tags Steam reported for the owned item, or the ones the definition sets.</summary>
            public string Tags => Owned != null && !string.IsNullOrEmpty(Owned.Tags) ? Owned.Tags : TagsConverter.ToString(Definition.Tags);
        }
    }
}
