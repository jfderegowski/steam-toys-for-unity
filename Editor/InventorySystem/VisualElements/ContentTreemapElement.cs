using System.Collections.Generic;
using System.Linq;
using SteamToys.Runtime.InventorySystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SteamToys.Editor.InventorySystem
{
    /// <summary>
    /// A treemap of a <see cref="BundleContent"/>, like a stock market map: one tile per entry, its area the entry's
    /// share. An entry whose item has content of its own (a generator, a bundle...) is drawn as a group, like a market
    /// sector: its name on top and its own entries tiled below, recursively down to the last level.
    /// <list type="bullet">
    /// <item>Under a generator the tiles split by weight and show the chance per roll, multiplied down the levels.</item>
    /// <item>Under a bundle the tiles split by quantity and show how many are granted.</item>
    /// <item>Clicking a tile pings its item.</item>
    /// </list>
    /// Read-only; the weights are edited in the list or the <see cref="WeightBarElement"/>.
    /// </summary>
    public class ContentTreemapElement : VisualElement
    {
        private const string ITEM_PROP_NAME = "_item";
        private const string QUANTITY_PROP_NAME = "_quantity";

        private const string NO_ITEM_LABEL = "(None)";
        private const string PATH_SEPARATOR = " › ";

        private const int MAP_HEIGHT = 240;
        private const int MAP_RADIUS = 3;
        private const int MAX_DEPTH = 8;
        private const float TILE_GAP = 1f;
        private const float GROUP_GAP = 2f;
        private const float HEADER_HEIGHT = 16f;
        private const float MIN_NEST_SIZE = 12f;
        private const float MIN_LABEL_WIDTH = 24f;
        private const float MIN_LABEL_HEIGHT = 12f;
        private const float TWO_LINES_HEIGHT = 26f;
        private const int HEADER_FONT_SIZE = 11;
        private const int MIN_FONT_SIZE = 9;
        private const int MAX_FONT_SIZE = 20;

        private static readonly Color BackgroundColor = new(0.09f, 0.09f, 0.09f);
        private static readonly Color HeaderTextColor = new(0.85f, 0.85f, 0.85f);

        private const float GOLDEN_RATIO = 0.618034f;

        private readonly SerializedObject _serializedObject;
        private readonly string _elementsPath;

        private readonly VisualElement _canvas;
        private readonly List<Node> _roots = new();

        private class Node
        {
            public ItemDef Item;
            public string Name;
            public string Path;
            public uint Weight;
            public uint SiblingTotal;
            public bool InBundle;
            public double Chance;
            public float Hue;
            public int Index;
            public int Depth;
            public bool ContentIsBundle;
            public string NotExpanded;
            public readonly List<Node> Children = new();
            public Rect Rect;
        }

        public ContentTreemapElement(SerializedProperty elements)
        {
            _serializedObject = elements.serializedObject;
            _elementsPath = elements.propertyPath;

            style.marginLeft = 3;
            style.marginTop = 2;
            style.marginBottom = 2;
            style.height = MAP_HEIGHT;

            _canvas = new VisualElement {
                style = {
                    flexGrow = 1,
                    overflow = Overflow.Hidden,
                    backgroundColor = BackgroundColor,
                    borderTopLeftRadius = MAP_RADIUS,
                    borderTopRightRadius = MAP_RADIUS,
                    borderBottomLeftRadius = MAP_RADIUS,
                    borderBottomRightRadius = MAP_RADIUS
                }
            };

            _canvas.RegisterCallback<GeometryChangedEvent>(evt => {
                if (evt.oldRect.size != evt.newRect.size)
                    Draw();
            });

            Add(_canvas);
            Refresh();
        }

        /// <summary>Rebuilds the tree from the serialized entries and the content of the items they point at.</summary>
        public void Refresh()
        {
            _roots.Clear();

            var elements = _serializedObject.FindProperty(_elementsPath);
            if (elements != null)
            {
                var entries = new List<(ItemDef item, uint quantity)>(elements.arraySize);
                for (var i = 0; i < elements.arraySize; i++)
                {
                    var element = elements.GetArrayElementAtIndex(i);
                    entries.Add((element.FindPropertyRelative(ITEM_PROP_NAME).objectReferenceValue as ItemDef,
                                 element.FindPropertyRelative(QUANTITY_PROP_NAME).uintValue));
                }

                var owner = _serializedObject.targetObject as ItemDef;
                var path = new HashSet<ItemDef>();
                if (owner)
                    path.Add(owner);

                AddChildren(_roots, entries, owner && owner.Type == ItemType.Bundle, 1.0, null, path, 0, null);
            }

            style.display = _roots.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            Draw();
        }

        #region Tree

        private static void AddChildren(List<Node> into, IReadOnlyList<(ItemDef item, uint quantity)> entries,
                                        bool inBundle, double chance, string parentPath, HashSet<ItemDef> path,
                                        int depth, float? hue)
        {
            uint total = 0;
            foreach (var (_, quantity) in entries)
                total += quantity;

            if (total == 0)
                return;

            for (var i = 0; i < entries.Count; i++)
            {
                var (item, quantity) = entries[i];
                if (quantity == 0)
                    continue;

                var name = GetItemName(item);
                var node = new Node {
                    Item = item,
                    Name = name,
                    Path = parentPath == null ? name : parentPath + PATH_SEPARATOR + name,
                    Weight = quantity,
                    SiblingTotal = total,
                    InBundle = inBundle,
                    Chance = inBundle ? chance : chance * quantity / total,
                    Hue = hue ?? i * GOLDEN_RATIO % 1f,
                    Index = i,
                    Depth = depth
                };

                if (item && item.SchemaSupportsContent() && item.Content is { Count: > 0 } content)
                {
                    if (path.Contains(item))
                        node.NotExpanded = "Contains itself higher up; not expanded again.";
                    else if (depth + 1 >= MAX_DEPTH)
                        node.NotExpanded = $"Nested deeper than {MAX_DEPTH} levels; not expanded.";
                    else
                    {
                        node.ContentIsBundle = item.Type == ItemType.Bundle;

                        path.Add(item);
                        AddChildren(node.Children, content.Select(e => (e.Item, e.Quantity)).ToList(),
                                    node.ContentIsBundle, node.Chance, node.Path, path, depth + 1, node.Hue);
                        path.Remove(item);
                    }
                }

                into.Add(node);
            }
        }

        #endregion

        #region Drawing

        private void Draw()
        {
            _canvas.Clear();

            var size = _canvas.contentRect.size;
            if (_roots.Count == 0 || float.IsNaN(size.x) || size.x <= 0 || size.y <= 0)
                return;

            var area = new Rect(0, 0, size.x, size.y);
            Squarify(_roots, area);

            foreach (var node in _roots)
                DrawNode(node);
        }

        /// <summary>
        /// Draws the node: a leaf as a colored tile, an item with content as a group, its name on top over the dark
        /// background and its own entries tiled below. Every element is a direct child of the canvas, so a child drawn
        /// later lies on top of its group and takes the click.
        /// </summary>
        private void DrawNode(Node node)
        {
            var nested = node.Children.Count > 0 && node.Rect.width >= MIN_NEST_SIZE + 2 * GROUP_GAP &&
                         node.Rect.height >= HEADER_HEIGHT + MIN_NEST_SIZE + 2 * GROUP_GAP;

            // Groups stand apart from their neighbours by a wider gap than the tiles inside them.
            var gap = nested ? GROUP_GAP : TILE_GAP;
            var box = new Rect(node.Rect.x + gap, node.Rect.y + gap, node.Rect.width - 2 * gap, node.Rect.height - 2 * gap);
            if (box.width <= 0 || box.height <= 0)
                return;

            var tile = new VisualElement {
                tooltip = Tooltip(node, nested),
                style = {
                    position = Position.Absolute,
                    left = box.x,
                    top = box.y,
                    width = box.width,
                    height = box.height,
                    overflow = Overflow.Hidden,
                    backgroundColor = nested ? BackgroundColor : TileColor(node)
                }
            };

            if (box.width >= MIN_LABEL_WIDTH && box.height >= MIN_LABEL_HEIGHT)
                tile.Add(nested ? HeaderLabel(node) : TileLabel(node, box));

            var item = node.Item;
            tile.RegisterCallback<PointerDownEvent>(evt => {
                if (evt.button != 0 || !item)
                    return;

                EditorGUIUtility.PingObject(item);
                evt.StopPropagation();
            });

            _canvas.Add(tile);

            if (!nested)
                return;

            Squarify(node.Children, new Rect(box.x, box.y + HEADER_HEIGHT, box.width, box.height - HEADER_HEIGHT));
            foreach (var child in node.Children)
                DrawNode(child);
        }

        private static Label HeaderLabel(Node node) => new($"{node.Name}  {Amount(node)}  ›") {
            pickingMode = PickingMode.Ignore,
            style = {
                height = HEADER_HEIGHT,
                fontSize = HEADER_FONT_SIZE,
                unityTextAlign = TextAnchor.MiddleLeft,
                whiteSpace = WhiteSpace.NoWrap,
                textOverflow = TextOverflow.Ellipsis,
                overflow = Overflow.Hidden,
                paddingLeft = 2,
                paddingRight = 2,
                marginLeft = 0,
                marginRight = 0,
                color = HeaderTextColor
            }
        };

        /// <summary>
        /// The name above the amount, or beside it on a low tile. Each line is a label of its own, because a label
        /// cuts only single-line text with an ellipsis; a too long name ends in "…" and the amount stays whole as
        /// long as there is room for it.
        /// </summary>
        private static VisualElement TileLabel(Node node, Rect box)
        {
            var twoLines = box.height >= TWO_LINES_HEIGHT;
            var amount = Amount(node);

            // Big tiles get big text, like the leaders of a market map; the name has to fit the width.
            var longest = twoLines ? Mathf.Max(node.Name.Length, amount.Length) : node.Name.Length + amount.Length + 1;
            var fontSize = Mathf.Min(box.width * 1.7f / Mathf.Max(6, longest), box.height / (twoLines ? 3.2f : 1.4f));
            fontSize = Mathf.Clamp(Mathf.Floor(fontSize), MIN_FONT_SIZE, MAX_FONT_SIZE);

            var lines = new VisualElement {
                pickingMode = PickingMode.Ignore,
                style = {
                    flexGrow = 1,
                    flexDirection = twoLines ? FlexDirection.Column : FlexDirection.Row,
                    justifyContent = Justify.Center,
                    alignItems = Align.Center,
                    overflow = Overflow.Hidden,
                    paddingLeft = 2,
                    paddingRight = 2
                }
            };

            var nameLine = LineLabel(node.Name, fontSize);
            var amountLine = LineLabel(amount, fontSize);

            // In a row the name gives way first; the amount shrinks only once the name is gone.
            nameLine.style.flexShrink = 1;
            amountLine.style.flexShrink = twoLines ? 1 : 0.01f;
            if (!twoLines)
                amountLine.style.marginLeft = fontSize * 0.3f;

            lines.Add(nameLine);
            lines.Add(amountLine);
            return lines;
        }

        private static Label LineLabel(string text, float fontSize) => new(text) {
            pickingMode = PickingMode.Ignore,
            style = {
                fontSize = fontSize,
                maxWidth = Length.Percent(100),
                minWidth = 0,
                unityTextAlign = TextAnchor.MiddleCenter,
                whiteSpace = WhiteSpace.NoWrap,
                textOverflow = TextOverflow.Ellipsis,
                overflow = Overflow.Hidden,
                paddingLeft = 0,
                paddingRight = 0,
                marginLeft = 0,
                marginRight = 0,
                marginTop = 0,
                marginBottom = 0,
                color = Color.white
            }
        };

        /// <summary>What the tile stands for in short: the count in a bundle, the chance per roll under a generator.</summary>
        private static string Amount(Node node)
        {
            if (!node.InBundle)
                return Percent(node.Chance);

            return node.Chance < 1 ? $"×{node.Weight} · {Percent(node.Chance)}" : $"×{node.Weight}";
        }

        private static string Tooltip(Node node, bool nested)
        {
            var lines = new List<string> { node.Path };

            lines.Add(node.InBundle
                ? $"×{node.Weight} in the bundle ({node.Weight} of {node.SiblingTotal} items)"
                : $"Weight {node.Weight} of {node.SiblingTotal} ({100.0 * node.Weight / node.SiblingTotal:0.##}%)");

            if (node.Chance < 1)
                lines.Add($"Chance per roll: {node.Chance * 100:0.###}%");

            if (node.Children.Count > 0)
                lines.Add(node.ContentIsBundle ? "Bundle: grants everything inside." : "Generator: grants one entry inside.");

            if (node.Children.Count > 0 && !nested)
                lines.Add("Too small to show its content.");

            if (node.NotExpanded != null)
                lines.Add(node.NotExpanded);

            if (node.Item)
                lines.Add("Click to ping.");

            return string.Join("\n", lines);
        }

        private static string Percent(double chance) => $"{chance * 100:0.#}%";

        private static Color TileColor(Node node)
        {
            var value = EditorGUIUtility.isProSkin ? 0.5f : 0.65f;

            // Below the first level siblings share their parent's hue, so their brightness tells them apart.
            if (node.Depth > 0)
                value += (node.Index % 3 - 1) * 0.07f;

            return Color.HSVToRGB(node.Hue, 0.45f, Mathf.Clamp01(value));
        }

        #endregion

        #region Layout

        /// <summary>
        /// Squarified treemap: lays the nodes into <paramref name="area"/> by weight, biggest first, filling rows along
        /// the shorter side for as long as that keeps the tiles closer to squares.
        /// </summary>
        private static void Squarify(List<Node> nodes, Rect area)
        {
            var sorted = nodes.OrderByDescending(n => n.Weight).ToList();

            double total = sorted.Sum(n => (double)n.Weight);
            if (total <= 0 || area.width <= 0 || area.height <= 0)
                return;

            var scale = area.width * area.height / total;
            var start = 0;

            while (start < sorted.Count)
            {
                var side = Mathf.Min(area.width, area.height);
                var end = start + 1;
                var rowArea = sorted[start].Weight * scale;
                var worst = Worst(sorted, start, end, rowArea, side, scale);

                while (end < sorted.Count)
                {
                    var nextArea = rowArea + sorted[end].Weight * scale;
                    var nextWorst = Worst(sorted, start, end + 1, nextArea, side, scale);
                    if (nextWorst > worst)
                        break;

                    rowArea = nextArea;
                    worst = nextWorst;
                    end++;
                }

                // The last row takes what is left, so rounding never leaves a sliver unfilled.
                var thickness = end == sorted.Count ? Mathf.Max(area.width, area.height) : (float)(rowArea / side);

                var offset = 0f;
                for (var i = start; i < end; i++)
                {
                    var length = (float)(sorted[i].Weight * scale / rowArea) * side;

                    sorted[i].Rect = area.width >= area.height
                        ? new Rect(area.x, area.y + offset, thickness, length)
                        : new Rect(area.x + offset, area.y, length, thickness);

                    offset += length;
                }

                area = area.width >= area.height
                    ? new Rect(area.x + thickness, area.y, area.width - thickness, area.height)
                    : new Rect(area.x, area.y + thickness, area.width, area.height - thickness);

                start = end;
            }
        }

        /// <summary>The worst aspect ratio in a row of the nodes in [<paramref name="start"/>, <paramref name="end"/>), sorted biggest first.</summary>
        private static double Worst(List<Node> sorted, int start, int end, double rowArea, float side, double scale)
        {
            var max = sorted[start].Weight * scale;
            var min = sorted[end - 1].Weight * scale;
            var side2 = (double)side * side;
            var row2 = rowArea * rowArea;

            return System.Math.Max(side2 * max / row2, row2 / (side2 * min));
        }

        #endregion

        private static string GetItemName(ItemDef item)
        {
            if (!item)
                return NO_ITEM_LABEL;

            return string.IsNullOrEmpty(item.Name) ? item.name : item.Name;
        }
    }
}
