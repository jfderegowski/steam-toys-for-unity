using System;
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
    /// The weights of a generator's entries can be edited here as in the <see cref="WeightBarElement"/>, at any level,
    /// in the asset that holds them:
    /// <list type="bullet">
    /// <item>Dragging the edge between two tiles of one row moves weight from one to the other.</item>
    /// <item>Double-clicking a tile types its share among its neighbours in percent; they keep their proportions.</item>
    /// <item>The context menu of a tile gives every entry of its level, or of the group itself, the same weight.</item>
    /// </list>
    /// A bundle's quantities are counts, not odds, so they are left to the list.
    /// </summary>
    public class ContentTreemapElement : VisualElement
    {
        private const string ITEM_PROP_NAME = "_item";
        private const string QUANTITY_PROP_NAME = "_quantity";
        private const string CONTENT_ELEMENTS_PATH = "_bundle._elements";

        private const string STYLE_SHEET_GUID = "e920035125f34fd78dd96a67fe2147fb";
        private const string HANDLE_COLUMNS_CLASS = "content-treemap__handle--columns";
        private const string HANDLE_ROWS_CLASS = "content-treemap__handle--rows";

        private const string NO_ITEM_LABEL = "(None)";
        private const string PATH_SEPARATOR = " › ";

        private const string DRAG_UNDO_NAME = "Change Weights";
        private const string PERCENT_UNDO_NAME = "Set Weight Share";
        private const string EQUALIZE_UNDO_NAME = "Equalize Weights";

        private const string HANDLE_TOOLTIP = "Drag to move weight between the two entries.";
        private const string EQUALIZE_LEVEL_MENU = "Equalize Weights on This Level";
        private const string EQUALIZE_INSIDE_MENU = "Equalize Weights Inside";

        private const int MAP_HEIGHT = 240;
        private const int MAP_RADIUS = 6;
        private const int CORNER_RADIUS = 5;
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

        private const float HANDLE_WIDTH = 5f;
        private const float PERCENT_FIELD_WIDTH = 70f;
        private const float PERCENT_FIELD_HEIGHT = 18f;
        private const uint MIN_DRAG_TOTAL = 100;
        private const uint MIN_PERCENT_TOTAL = 1000;
        private const float MIN_PERCENT = 0.1f;
        private const float MAX_PERCENT = 99.9f;

        private static readonly Color BackgroundColor = new(0.09f, 0.09f, 0.09f);
        private static readonly Color HeaderTextColor = new(0.85f, 0.85f, 0.85f);
        private static readonly Color HandleIdleColor = new(1f, 1f, 1f, 0f);
        private static readonly Color HandleActiveColor = new(1f, 1f, 1f, 0.5f);

        private const float GOLDEN_RATIO = 0.618034f;

        private readonly SerializedObject _serializedObject;
        private readonly string _elementsPath;

        private readonly VisualElement _canvas;
        private readonly VisualElement _overlay;
        private readonly List<Node> _roots = new();
        private readonly List<VisualElement> _rootDrawn = new();
        private readonly Dictionary<ItemDef, SerializedObject> _sources = new();

        private Node _dragA;
        private Node _dragB;
        private VisualElement _dragHandle;
        private bool _dragVertical;
        private uint[] _dragStartWeights;
        private uint _dragPair;
        private uint _dragTotal;
        private uint _dragScale;
        private float _dragStartPos;
        private float _dragLengthA;
        private float _dragPairLength;
        private bool _dragMoved;
        private int _undoGroup;

        private FloatField _percentField;
        private bool _percentCancelled;

        private class Node
        {
            public ItemDef Item;
            public string Name;
            public string Path;
            public uint Weight;
            public uint SiblingTotal;
            public bool InBundle;
            public double ParentChance;
            public double Chance;
            public float Hue;
            public int Index;
            public int Depth;
            public bool ContentIsBundle;
            public string NotExpanded;
            public readonly List<Node> Children = new();

            /// <summary>The object and the list this entry is in; null when it cannot be edited.</summary>
            public SerializedObject Source;
            public string SourcePath;

            /// <summary>The object and the list of the item's own entries; null when they cannot be edited.</summary>
            public SerializedObject ContentSource;

            public Rect Rect;
            public Rect Box;
            public Node RowNext;
            public Node NextRowAlone;
            public bool RowStacked;
            public readonly List<VisualElement> Drawn = new();

            public bool Editable => Source != null && !InBundle;
        }

        public ContentTreemapElement(SerializedProperty elements)
        {
            _serializedObject = elements.serializedObject;
            _elementsPath = elements.propertyPath;

            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(AssetDatabase.GUIDToAssetPath(STYLE_SHEET_GUID));
            if (styleSheet)
                styleSheets.Add(styleSheet);

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

            // The handles and the typed share lie over every tile, so a tile drawn later cannot cover them.
            _overlay = new VisualElement {
                pickingMode = PickingMode.Ignore,
                style = { position = Position.Absolute, left = 0, right = 0, top = 0, bottom = 0 }
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
            // A drag redraws only the two tiles it changes and refreshes everything once it ends.
            if (_dragA != null)
                return;

            _roots.Clear();
            _sources.Clear();

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

                AddChildren(_roots, entries, owner && owner.Type == ItemType.Bundle, 1.0, null, path, 0, null,
                            _serializedObject, _elementsPath);
            }

            style.display = _roots.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            Draw();
        }

        #region Tree

        private void AddChildren(List<Node> into, IReadOnlyList<(ItemDef item, uint quantity)> entries, bool inBundle,
                                 double chance, string parentPath, HashSet<ItemDef> path, int depth, float? hue,
                                 SerializedObject source, string sourcePath)
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
                    ParentChance = chance,
                    Chance = inBundle ? chance : chance * quantity / total,
                    Hue = hue ?? i * GOLDEN_RATIO % 1f,
                    Index = i,
                    Depth = depth,
                    Source = source,
                    SourcePath = sourcePath
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
                        node.ContentSource = node.ContentIsBundle ? null : GetSource(item);

                        path.Add(item);
                        AddChildren(node.Children, content.Select(e => (e.Item, e.Quantity)).ToList(),
                                    node.ContentIsBundle, node.Chance, node.Path, path, depth + 1, node.Hue,
                                    node.ContentSource, CONTENT_ELEMENTS_PATH);
                        path.Remove(item);
                    }
                }

                into.Add(node);
            }
        }

        /// <summary>One serialized object per item, shared by every place the item shows up in.</summary>
        private SerializedObject GetSource(ItemDef item)
        {
            if (_sources.TryGetValue(item, out var source))
                return source;

            source = new SerializedObject(item);
            if (source.FindProperty(CONTENT_ELEMENTS_PATH) == null)
                source = null;

            _sources[item] = source;
            return source;
        }

        private static void RecomputeChance(Node node)
        {
            node.Chance = node.InBundle ? node.ParentChance : node.ParentChance * node.Weight / node.SiblingTotal;

            foreach (var child in node.Children)
            {
                child.ParentChance = node.Chance;
                RecomputeChance(child);
            }
        }

        #endregion

        #region Drawing

        private void Draw()
        {
            if (_dragA != null)
                return;

            EndPercentEdit(false);

            _canvas.Clear();
            _overlay.Clear();
            _rootDrawn.Clear();

            var size = _canvas.contentRect.size;
            if (_roots.Count == 0 || float.IsNaN(size.x) || size.x <= 0 || size.y <= 0)
                return;

            Squarify(_roots, new Rect(0, 0, size.x, size.y));

            foreach (var node in _roots)
                DrawNode(node);

            AddHandles(_roots, _rootDrawn);
            _canvas.Add(_overlay);
        }

        /// <summary>
        /// Draws the node: a leaf as a colored tile, an item with content as a group, its name on top over the dark
        /// background and its own entries tiled below. Every element is a direct child of the canvas, so a child drawn
        /// later lies on top of its group and takes the click.
        /// </summary>
        private void DrawNode(Node node)
        {
            node.Drawn.Clear();

            var nested = node.Children.Count > 0 && node.Rect.width >= MIN_NEST_SIZE + 2 * GROUP_GAP &&
                         node.Rect.height >= HEADER_HEIGHT + MIN_NEST_SIZE + 2 * GROUP_GAP;

            // Groups stand apart from their neighbours by a wider gap than the tiles inside them.
            var gap = nested ? GROUP_GAP : TILE_GAP;
            var box = new Rect(node.Rect.x + gap, node.Rect.y + gap, node.Rect.width - 2 * gap, node.Rect.height - 2 * gap);
            node.Box = box;
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

            RoundOuterCorners(tile, node, box);

            if (box.width >= MIN_LABEL_WIDTH && box.height >= MIN_LABEL_HEIGHT)
                tile.Add(nested ? HeaderLabel(node) : TileLabel(node, box));

            tile.RegisterCallback<PointerDownEvent>(evt => {
                if (evt.button != 0)
                    return;

                if (evt.clickCount == 2 && node.Editable)
                    BeginPercentEdit(node);
                else if (node.Item)
                    EditorGUIUtility.PingObject(node.Item);

                evt.StopPropagation();
            });

            tile.AddManipulator(new ContextualMenuManipulator(evt => {
                if (node.Editable)
                    evt.menu.AppendAction(EQUALIZE_LEVEL_MENU, _ => Equalize(node.Source, node.SourcePath));

                if (node.ContentSource != null)
                    evt.menu.AppendAction(EQUALIZE_INSIDE_MENU, _ => Equalize(node.ContentSource, CONTENT_ELEMENTS_PATH));
            }));

            _canvas.Add(tile);
            node.Drawn.Add(tile);

            if (!nested)
                return;

            Squarify(node.Children, new Rect(box.x, box.y + HEADER_HEIGHT, box.width, box.height - HEADER_HEIGHT));
            foreach (var child in node.Children)
                DrawNode(child);

            AddHandles(node.Children, node.Drawn);
        }

        /// <summary>
        /// Rounds only the corners of the whole map: a tile gets a round corner where it lies in a corner of the canvas,
        /// at most the gaps of its groups away from it.
        /// </summary>
        private void RoundOuterCorners(VisualElement tile, Node node, Rect box)
        {
            var size = _canvas.contentRect.size;
            var reach = (node.Depth + 1) * GROUP_GAP + 0.5f;

            var left = box.xMin <= reach;
            var top = box.yMin <= reach;
            var right = box.xMax >= size.x - reach;
            var bottom = box.yMax >= size.y - reach;

            if (left && top) tile.style.borderTopLeftRadius = CORNER_RADIUS;
            if (right && top) tile.style.borderTopRightRadius = CORNER_RADIUS;
            if (left && bottom) tile.style.borderBottomLeftRadius = CORNER_RADIUS;
            if (right && bottom) tile.style.borderBottomRightRadius = CORNER_RADIUS;
        }

        /// <summary>Removes everything drawn for the node and its children, their handles included.</summary>
        private static void Erase(Node node)
        {
            foreach (var element in node.Drawn)
                element.RemoveFromHierarchy();

            node.Drawn.Clear();

            foreach (var child in node.Children)
                Erase(child);
        }

        private static Label HeaderLabel(Node node) => new($"{node.Name}  {Amount(node)}") {
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

        #region Dragging

        /// <summary>
        /// A handle on every edge that two editable tiles share whole: between neighbours in one row, and between two
        /// rows of one tile each. Moving such an edge changes only those two tiles.
        /// </summary>
        private void AddHandles(List<Node> nodes, List<VisualElement> owner)
        {
            foreach (var node in nodes)
            {
                if (!node.Editable)
                    continue;

                if (node.RowNext is { Editable: true } next)
                    AddHandle(node, next, node.RowStacked);

                if (node.NextRowAlone is { Editable: true } below)
                    AddHandle(node, below, !node.RowStacked);
            }

            void AddHandle(Node a, Node b, bool vertical)
            {
                var handle = CreateHandle(a, b, vertical);
                _overlay.Add(handle);
                owner.Add(handle);
            }
        }

        private VisualElement CreateHandle(Node a, Node b, bool vertical)
        {
            var handle = new VisualElement {
                tooltip = HANDLE_TOOLTIP,
                style = { position = Position.Absolute, backgroundColor = HandleIdleColor }
            };
            handle.AddToClassList(vertical ? HANDLE_ROWS_CLASS : HANDLE_COLUMNS_CLASS);
            PlaceHandle(handle, a, vertical);

            handle.RegisterCallback<PointerEnterEvent>(_ => handle.style.backgroundColor = HandleActiveColor);
            handle.RegisterCallback<PointerLeaveEvent>(_ => {
                if (!handle.HasPointerCapture(PointerId.mousePointerId))
                    handle.style.backgroundColor = HandleIdleColor;
            });

            handle.RegisterCallback<PointerDownEvent>(evt => {
                if (evt.button != 0)
                    return;

                if (BeginDrag(a, b, handle, vertical, vertical ? evt.position.y : evt.position.x))
                    handle.CapturePointer(evt.pointerId);

                evt.StopPropagation();
            });

            handle.RegisterCallback<PointerMoveEvent>(evt => {
                if (_dragHandle != handle || !handle.HasPointerCapture(evt.pointerId))
                    return;

                Drag(vertical ? evt.position.y : evt.position.x);
                evt.StopPropagation();
            });

            handle.RegisterCallback<PointerUpEvent>(evt => {
                if (!handle.HasPointerCapture(evt.pointerId))
                    return;

                handle.ReleasePointer(evt.pointerId);
                evt.StopPropagation();
            });

            handle.RegisterCallback<PointerCaptureOutEvent>(_ => {
                handle.style.backgroundColor = HandleIdleColor;
                EndDrag();
            });

            return handle;
        }

        /// <summary>Lays the handle along the edge after <paramref name="a"/>: its bottom edge when dragged vertically, else its right one.</summary>
        private static void PlaceHandle(VisualElement handle, Node a, bool vertical)
        {
            if (vertical)
            {
                handle.style.left = a.Rect.x;
                handle.style.width = a.Rect.width;
                handle.style.top = a.Rect.yMax - HANDLE_WIDTH / 2f;
                handle.style.height = HANDLE_WIDTH;
            }
            else
            {
                handle.style.top = a.Rect.y;
                handle.style.height = a.Rect.height;
                handle.style.left = a.Rect.xMax - HANDLE_WIDTH / 2f;
                handle.style.width = HANDLE_WIDTH;
            }
        }

        private bool BeginDrag(Node a, Node b, VisualElement handle, bool vertical, float pointer)
        {
            EndPercentEdit(true);

            a.Source.Update();
            var elements = a.Source.FindProperty(a.SourcePath);
            if (elements == null || Math.Max(a.Index, b.Index) >= elements.arraySize)
                return false;

            var weights = ReadWeights(elements);
            var pair = weights[a.Index] + weights[b.Index];
            if (pair == 0)
                return false;

            // With a small pair one pixel is too coarse a step, so every weight of the level is first multiplied by
            // the same integer (the odds stay the same) and reduced again on release.
            _dragScale = pair < MIN_DRAG_TOTAL ? (MIN_DRAG_TOTAL + pair - 1) / pair : 1;
            for (var i = 0; i < weights.Length; i++)
                weights[i] *= _dragScale;

            _dragA = a;
            _dragB = b;
            _dragHandle = handle;
            _dragVertical = vertical;
            _dragStartWeights = weights;
            _dragPair = pair * _dragScale;
            _dragTotal = Sum(weights);
            _dragStartPos = pointer;
            _dragLengthA = vertical ? a.Rect.height : a.Rect.width;
            _dragPairLength = _dragLengthA + (vertical ? b.Rect.height : b.Rect.width);
            _dragMoved = false;

            BeginUndo(DRAG_UNDO_NAME);
            if (_dragScale > 1)
                WriteWeights(a.Source, elements, weights);

            return true;
        }

        private void Drag(float pointer)
        {
            if (_dragPairLength <= 0)
                return;

            var a = _dragA;
            var b = _dragB;

            // Neither entry drops to zero, which would take it out of the odds, unless the two had nothing to share.
            var min = _dragPair >= 2 ? 1L : 0L;
            var lengthA = _dragLengthA + pointer - _dragStartPos;
            var weightA = (uint)Math.Clamp((long)Math.Round(lengthA / _dragPairLength * _dragPair), min, _dragPair - min);
            if (_dragMoved && weightA == a.Weight)
                return;

            var weights = (uint[])_dragStartWeights.Clone();
            weights[a.Index] = weightA;
            weights[b.Index] = _dragPair - weightA;

            a.Source.Update();
            var elements = a.Source.FindProperty(a.SourcePath);
            WriteWeights(a.Source, elements, weights);
            _dragMoved = true;

            a.Weight = weights[a.Index];
            b.Weight = weights[b.Index];
            a.SiblingTotal = b.SiblingTotal = _dragTotal;
            RecomputeChance(a);
            RecomputeChance(b);

            ResizePair();
        }

        /// <summary>Gives the two tiles their new share of the row and redraws only them, so the handle keeps the pointer.</summary>
        private void ResizePair()
        {
            var a = _dragA;
            var b = _dragB;
            var lengthA = _dragPairLength * a.Weight / _dragPair;

            if (_dragVertical)
            {
                a.Rect = new Rect(a.Rect.x, a.Rect.y, a.Rect.width, lengthA);
                b.Rect = new Rect(b.Rect.x, a.Rect.yMax, b.Rect.width, _dragPairLength - lengthA);
            }
            else
            {
                a.Rect = new Rect(a.Rect.x, a.Rect.y, lengthA, a.Rect.height);
                b.Rect = new Rect(a.Rect.xMax, b.Rect.y, _dragPairLength - lengthA, b.Rect.height);
            }

            Erase(a);
            Erase(b);
            DrawNode(a);
            DrawNode(b);

            PlaceHandle(_dragHandle, a, _dragVertical);
            _overlay.BringToFront();
        }

        private void EndDrag()
        {
            if (_dragA == null)
                return;

            var source = _dragA.Source;
            var path = _dragA.SourcePath;

            _dragA = null;
            _dragB = null;
            _dragHandle = null;

            // Moved weights come back as the smallest numbers with the same odds; a click without a move
            // only takes out what it multiplied in, so the weights typed by hand keep their numbers.
            if (_dragMoved)
                ReduceWeights(source, path, 0);
            else if (_dragScale > 1)
                ReduceWeights(source, path, _dragScale);

            Undo.CollapseUndoOperations(_undoGroup);
            Refresh();
        }

        #endregion

        #region Typed Share

        private void BeginPercentEdit(Node node)
        {
            EndPercentEdit(true);

            node.Source.Update();
            var elements = node.Source.FindProperty(node.SourcePath);
            if (elements == null || elements.arraySize < 2 || node.Index >= elements.arraySize)
                return;

            var weights = ReadWeights(elements);
            var total = Sum(weights);
            if (total == 0)
                return;

            var size = _canvas.contentRect.size;
            var width = Mathf.Min(PERCENT_FIELD_WIDTH, size.x);
            var height = Mathf.Min(PERCENT_FIELD_HEIGHT, size.y);

            _percentCancelled = false;
            _percentField = new FloatField {
                value = (float)Math.Round(100.0 * weights[node.Index] / total, 1),
                tooltip = "Share among the entries of its level, in percent.",
                style = {
                    position = Position.Absolute,
                    width = width,
                    height = height,
                    left = Mathf.Clamp(node.Box.center.x - width / 2f, 0, size.x - width),
                    top = Mathf.Clamp(node.Box.center.y - height / 2f, 0, size.y - height),
                    marginLeft = 0,
                    marginRight = 0,
                    marginTop = 0,
                    marginBottom = 0
                }
            };

            var field = _percentField;
            field.RegisterCallback<KeyDownEvent>(evt => {
                switch (evt.keyCode)
                {
                    case KeyCode.Return:
                    case KeyCode.KeypadEnter:
                        field.Blur();
                        evt.StopPropagation();
                        break;
                    case KeyCode.Escape:
                        _percentCancelled = true;
                        field.Blur();
                        evt.StopPropagation();
                        break;
                }
            }, TrickleDown.TrickleDown);

            field.RegisterCallback<FocusOutEvent>(_ => {
                if (_percentField != field)
                    return;

                EndPercentEdit(!_percentCancelled, node);
            });

            _overlay.Add(field);
            field.schedule.Execute(() => field.Focus());
        }

        private void EndPercentEdit(bool apply, Node node = null)
        {
            if (_percentField == null)
                return;

            var field = _percentField;
            _percentField = null;
            field.RemoveFromHierarchy();

            if (apply && node != null)
                SetShare(node, field.value);
        }

        /// <summary>Gives the entry this share of its level; the other entries keep their proportions.</summary>
        private void SetShare(Node node, float percent)
        {
            node.Source.Update();
            var elements = node.Source.FindProperty(node.SourcePath);
            if (elements == null || node.Index >= elements.arraySize || elements.arraySize < 2)
                return;

            percent = Mathf.Clamp(percent, MIN_PERCENT, MAX_PERCENT);

            var weights = ReadWeights(elements);
            var others = Sum(weights) - weights[node.Index];

            if (others == 0)
            {
                for (var i = 0; i < weights.Length; i++)
                    if (i != node.Index) weights[i] = 1;

                others = (uint)(weights.Length - 1);
            }

            // Enough resolution for a tenth of a percent before the weights are reduced again.
            var scale = others < MIN_PERCENT_TOTAL ? (MIN_PERCENT_TOTAL + others - 1) / others : 1;
            for (var i = 0; i < weights.Length; i++)
                weights[i] *= scale;

            var otherWeight = (double)others * scale;
            weights[node.Index] = (uint)Math.Max(1, Math.Round(percent * otherWeight / (100 - percent)));

            BeginUndo(PERCENT_UNDO_NAME);
            WriteWeights(node.Source, elements, weights);
            ReduceWeights(node.Source, node.SourcePath, 0);
            Undo.CollapseUndoOperations(_undoGroup);

            Refresh();
        }

        #endregion

        private void Equalize(SerializedObject source, string path)
        {
            source.Update();
            var elements = source.FindProperty(path);
            if (elements == null)
                return;

            var weights = new uint[elements.arraySize];
            Array.Fill(weights, 1u);

            BeginUndo(EQUALIZE_UNDO_NAME);
            WriteWeights(source, elements, weights);
            Undo.CollapseUndoOperations(_undoGroup);

            Refresh();
        }

        #region Weights

        private void BeginUndo(string name)
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(name);
            _undoGroup = Undo.GetCurrentGroup();
        }

        /// <summary>Divides every weight by their greatest common divisor, taken together with <paramref name="limit"/> (0 for none).</summary>
        private static void ReduceWeights(SerializedObject source, string path, uint limit)
        {
            source.Update();
            var elements = source.FindProperty(path);
            if (elements == null)
                return;

            var weights = ReadWeights(elements);

            var divisor = limit;
            foreach (var weight in weights)
                divisor = Gcd(divisor, weight);

            if (divisor <= 1)
                return;

            for (var i = 0; i < weights.Length; i++)
                weights[i] /= divisor;

            WriteWeights(source, elements, weights);
        }

        private static uint[] ReadWeights(SerializedProperty elements)
        {
            var weights = new uint[elements.arraySize];
            for (var i = 0; i < weights.Length; i++)
                weights[i] = elements.GetArrayElementAtIndex(i).FindPropertyRelative(QUANTITY_PROP_NAME).uintValue;

            return weights;
        }

        private static void WriteWeights(SerializedObject source, SerializedProperty elements, uint[] weights)
        {
            for (var i = 0; i < weights.Length; i++)
                elements.GetArrayElementAtIndex(i).FindPropertyRelative(QUANTITY_PROP_NAME).uintValue = weights[i];

            source.ApplyModifiedProperties();
        }

        private static uint Sum(uint[] weights)
        {
            uint total = 0;
            foreach (var weight in weights)
                total += weight;

            return total;
        }

        private static uint Gcd(uint a, uint b)
        {
            while (b != 0)
                (a, b) = (b, a % b);

            return a;
        }

        #endregion

        #region Layout

        /// <summary>
        /// Squarified treemap: lays the nodes into <paramref name="area"/> by weight, biggest first, filling rows along
        /// the shorter side for as long as that keeps the tiles closer to squares. Each node also learns its neighbour
        /// in the row, for the handle between them.
        /// </summary>
        private static void Squarify(List<Node> nodes, Rect area)
        {
            var sorted = nodes.OrderByDescending(n => n.Weight).ToList();
            foreach (var node in sorted)
            {
                node.RowNext = null;
                node.NextRowAlone = null;
            }

            double total = sorted.Sum(n => (double)n.Weight);
            if (total <= 0 || area.width <= 0 || area.height <= 0)
                return;

            var scale = area.width * area.height / total;
            var start = 0;
            Node previousAlone = null;

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
                var stacked = area.width >= area.height;

                var offset = 0f;
                for (var i = start; i < end; i++)
                {
                    var length = (float)(sorted[i].Weight * scale / rowArea) * side;

                    sorted[i].Rect = stacked
                        ? new Rect(area.x, area.y + offset, thickness, length)
                        : new Rect(area.x + offset, area.y, length, thickness);
                    sorted[i].RowStacked = stacked;
                    sorted[i].RowNext = i + 1 < end ? sorted[i + 1] : null;

                    offset += length;
                }

                // Two rows of one tile each, laid the same way, share a whole edge across the rows.
                var alone = end - start == 1 ? sorted[start] : null;
                if (alone != null && previousAlone != null && previousAlone.RowStacked == stacked)
                    previousAlone.NextRowAlone = alone;

                previousAlone = alone;

                area = stacked
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

            return Math.Max(side2 * max / row2, row2 / (side2 * min));
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
