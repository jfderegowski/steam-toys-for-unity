using System;
using System.Collections.Generic;
using SteamToys.Runtime.InventorySystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SteamToys.Editor.InventorySystem
{
    /// <summary>
    /// A horizontal bar of the odds of a generator's <see cref="BundleContent"/>: one segment per entry, as wide as its
    /// share of the total weight.
    /// <list type="bullet">
    /// <item>Dragging the divider between two segments moves weight from one to the other; with Shift it moves weight
    /// between everything on either side, each side keeping its own proportions.</item>
    /// <item>Double-clicking a segment types its share in percent; the other entries keep their proportions.</item>
    /// <item>The reset button gives every entry the same weight.</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// With a small total one pixel is too coarse a step, so a drag first multiplies every weight by the same integer
    /// (the odds stay the same), and on release the weights are reduced to the smallest numbers with the same odds.
    /// </remarks>
    public class WeightBarElement : VisualElement
    {
        private const string ITEM_PROP_NAME = "_item";
        private const string QUANTITY_PROP_NAME = "_quantity";

        private const string STYLE_SHEET_GUID = "b15f860607b8448bab536d7468a09ea4";
        private const string HANDLE_CLASS = "weight-bar__handle";

        private const string NO_ITEM_LABEL = "(None)";
        private const string DRAG_UNDO_NAME = "Change Weights";
        private const string PERCENT_UNDO_NAME = "Set Weight Share";
        private const string EQUALIZE_UNDO_NAME = "Equalize Weights";

        private const string HANDLE_TOOLTIP =
            "Drag to move weight between the two entries.\nWith Shift, between everything on either side.";
        private const string EQUALIZE_ICON = "Refresh";
        private const string EQUALIZE_TOOLTIP = "Gives every entry the same weight.";

        private const int BAR_HEIGHT = 20;
        private const int BAR_RADIUS = 3;
        private const int HANDLE_WIDTH = 6;
        private const int PERCENT_FIELD_WIDTH = 70;
        private const uint MIN_DRAG_TOTAL = 100;
        private const uint MIN_PERCENT_TOTAL = 1000;
        private const uint SIDES_RESOLUTION = 1000;
        private const float MIN_PERCENT = 0.1f;
        private const float MAX_PERCENT = 99.9f;

        private const float GOLDEN_RATIO = 0.618034f;

        private readonly SerializedObject _serializedObject;
        private readonly string _elementsPath;

        private readonly VisualElement _bar;
        private readonly VisualElement _handleLayer;
        private readonly List<VisualElement> _segments = new();
        private readonly List<Label> _labels = new();
        private readonly List<VisualElement> _handles = new();

        private int _dragIndex = -1;
        private bool _dragAcrossSides;
        private bool _dragMoved;
        private float _dragStartX;
        private uint[] _dragStartWeights;
        private uint _dragTotal;
        private uint _dragScale;
        private int _undoGroup;

        private FloatField _percentField;
        private bool _percentCancelled;

        public WeightBarElement(SerializedProperty elements)
        {
            _serializedObject = elements.serializedObject;
            _elementsPath = elements.propertyPath;

            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(AssetDatabase.GUIDToAssetPath(STYLE_SHEET_GUID));
            if (styleSheet)
                styleSheets.Add(styleSheet);

            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.marginLeft = 3;
            style.marginTop = 1;
            style.marginBottom = 1;

            _bar = new VisualElement {
                style = {
                    flexGrow = 1,
                    flexDirection = FlexDirection.Row,
                    height = BAR_HEIGHT,
                    overflow = Overflow.Hidden,
                    borderTopLeftRadius = BAR_RADIUS,
                    borderTopRightRadius = BAR_RADIUS,
                    borderBottomLeftRadius = BAR_RADIUS,
                    borderBottomRightRadius = BAR_RADIUS
                }
            };

            // The handles lie over the segments, so a segment drawn later cannot cover the handle before it.
            _handleLayer = new VisualElement {
                pickingMode = PickingMode.Ignore,
                style = { position = Position.Absolute, left = 0, right = 0, top = 0, bottom = 0 }
            };

            Add(_bar);
            Add(new Button(Equalize) {
                iconImage = Background.FromTexture2D((Texture2D)EditorGUIUtility.IconContent(EQUALIZE_ICON).image),
                tooltip = EQUALIZE_TOOLTIP
            });

            Refresh();
        }

        /// <summary>Redraws the bar from the serialized weights; the segments are recreated only when their count changes.</summary>
        public void Refresh()
        {
            var elements = _serializedObject.FindProperty(_elementsPath);
            var isBundle = _serializedObject.targetObject is ItemDef { Type: ItemType.Bundle };

            if (elements == null || isBundle || elements.arraySize == 0)
            {
                style.display = DisplayStyle.None;
                return;
            }

            var count = elements.arraySize;
            var weights = ReadWeights(elements);
            var total = Sum(weights, 0, count);

            style.display = total > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            if (total == 0)
                return;

            if (_segments.Count != count)
                Rebuild(count);

            var start = 0f;
            for (var i = 0; i < count; i++)
            {
                var share = (float)weights[i] / total;
                var name = GetItemName(elements.GetArrayElementAtIndex(i));

                _segments[i].style.flexGrow = weights[i];
                _segments[i].tooltip = $"{name}: weight {weights[i]} of {total} ({share * 100:0.##}%)\n" +
                                       "Double-click to type its share.";
                _labels[i].text = $"{name} {share * 100:0.#}%";

                start += share;
                if (i < _handles.Count)
                    _handles[i].style.left = Length.Percent(start * 100);
            }
        }

        #region Building

        private void Rebuild(int count)
        {
            EndPercentEdit(false);

            _bar.Clear();
            _handleLayer.Clear();
            _segments.Clear();
            _labels.Clear();
            _handles.Clear();

            for (var i = 0; i < count; i++)
            {
                var segment = CreateSegment(i);
                var label = new Label {
                    pickingMode = PickingMode.Ignore,
                    style = {
                        unityTextAlign = TextAnchor.MiddleCenter,
                        overflow = Overflow.Hidden,
                        textOverflow = TextOverflow.Ellipsis,
                        whiteSpace = WhiteSpace.NoWrap,
                        marginLeft = 0,
                        marginRight = 0,
                        paddingLeft = 2,
                        paddingRight = 2,
                        color = Color.white
                    }
                };

                // The bar's own clipping does not round the far end, so the end segments carry the corners.
                if (i == 0)
                {
                    segment.style.borderTopLeftRadius = BAR_RADIUS;
                    segment.style.borderBottomLeftRadius = BAR_RADIUS;
                }

                if (i == count - 1)
                {
                    segment.style.borderTopRightRadius = BAR_RADIUS;
                    segment.style.borderBottomRightRadius = BAR_RADIUS;
                }

                segment.Add(label);
                _bar.Add(segment);
                _segments.Add(segment);
                _labels.Add(label);

                if (i < count - 1)
                    _handles.Add(CreateHandle(i));
            }

            _bar.Add(_handleLayer);
        }

        private VisualElement CreateSegment(int index)
        {
            var segment = new VisualElement {
                style = {
                    flexBasis = 0,
                    minWidth = 0,
                    overflow = Overflow.Hidden,
                    justifyContent = Justify.Center,
                    backgroundColor = SegmentColor(index)
                }
            };

            segment.RegisterCallback<PointerDownEvent>(evt => {
                if (evt.button != 0 || evt.clickCount != 2)
                    return;

                BeginPercentEdit(index);
                evt.StopPropagation();
            });

            return segment;
        }

        private VisualElement CreateHandle(int index)
        {
            var handle = new VisualElement {
                tooltip = HANDLE_TOOLTIP,
                style = {
                    position = Position.Absolute,
                    top = 0,
                    bottom = 0,
                    width = HANDLE_WIDTH,
                    marginLeft = -HANDLE_WIDTH / 2f
                }
            };
            handle.AddToClassList(HANDLE_CLASS);

            var idle = new Color(0f, 0f, 0f, 0.35f);
            var active = new Color(1f, 1f, 1f, 0.5f);
            handle.style.backgroundColor = idle;

            handle.RegisterCallback<PointerEnterEvent>(_ => handle.style.backgroundColor = active);
            handle.RegisterCallback<PointerLeaveEvent>(_ => {
                if (!handle.HasPointerCapture(PointerId.mousePointerId))
                    handle.style.backgroundColor = idle;
            });

            handle.RegisterCallback<PointerDownEvent>(evt => {
                if (evt.button != 0)
                    return;

                BeginDrag(index, evt.position.x, evt.shiftKey);
                handle.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            });

            handle.RegisterCallback<PointerMoveEvent>(evt => {
                if (_dragIndex != index || !handle.HasPointerCapture(evt.pointerId))
                    return;

                Drag(evt.position.x);
                evt.StopPropagation();
            });

            handle.RegisterCallback<PointerUpEvent>(evt => {
                if (!handle.HasPointerCapture(evt.pointerId))
                    return;

                handle.ReleasePointer(evt.pointerId);
                evt.StopPropagation();
            });

            handle.RegisterCallback<PointerCaptureOutEvent>(_ => {
                handle.style.backgroundColor = idle;
                EndDrag();
            });

            _handleLayer.Add(handle);
            return handle;
        }

        #endregion

        #region Dragging

        private void BeginDrag(int index, float pointerX, bool acrossSides)
        {
            EndPercentEdit(true);

            _serializedObject.Update();
            var elements = _serializedObject.FindProperty(_elementsPath);
            var weights = ReadWeights(elements);
            var total = Sum(weights, 0, weights.Length);

            BeginUndo(DRAG_UNDO_NAME);

            _dragScale = total < MIN_DRAG_TOTAL ? (MIN_DRAG_TOTAL + total - 1) / total : 1;
            if (_dragScale > 1)
            {
                for (var i = 0; i < weights.Length; i++)
                    weights[i] *= _dragScale;

                WriteWeights(elements, weights);
            }

            _dragIndex = index;
            _dragAcrossSides = acrossSides;
            _dragMoved = false;
            _dragStartX = pointerX;
            _dragStartWeights = weights;
            _dragTotal = total * _dragScale;
        }

        private void Drag(float pointerX)
        {
            var width = _bar.contentRect.width;
            if (width <= 0)
                return;

            var weights = _dragAcrossSides
                ? DragSides((pointerX - _dragStartX) / width)
                : DragPair((long)Math.Round((pointerX - _dragStartX) / width * _dragTotal));

            _serializedObject.Update();
            var elements = _serializedObject.FindProperty(_elementsPath);
            if (SameWeights(elements, weights))
                return;

            WriteWeights(elements, weights);
            _dragMoved = true;

            Refresh();
        }

        /// <summary>Moves weight between the two entries next to the divider; the others stay as they are.</summary>
        private uint[] DragPair(long delta)
        {
            var weights = (uint[])_dragStartWeights.Clone();
            var first = weights[_dragIndex];
            var pair = first + weights[_dragIndex + 1];

            // Neither entry drops to zero, which would take it out of the odds, unless the two had nothing to share.
            var min = pair >= 2 ? 1L : 0L;
            weights[_dragIndex] = (uint)Math.Clamp(first + delta, min, pair - min);
            weights[_dragIndex + 1] = pair - weights[_dragIndex];

            return weights;
        }

        /// <summary>
        /// Moves the divider by <paramref name="shift"/> of the bar width, scaling every entry up to it as one side
        /// and every entry after it as the other. Each side is an exact multiple of its own proportions, so the weights
        /// reduce to small numbers whenever the divider lands on a round share.
        /// </summary>
        private uint[] DragSides(float shift)
        {
            var weights = (uint[])_dragStartWeights.Clone();
            var split = _dragIndex + 1;

            var left = ReduceRange(weights, 0, split);
            var right = ReduceRange(weights, split, weights.Length);
            if (left == 0 || right == 0)
                return weights;

            var leftShare = Mathf.Clamp01((float)Sum(_dragStartWeights, 0, split) / _dragTotal + shift);
            var leftScale = (uint)Math.Max(1, Math.Round(leftShare * SIDES_RESOLUTION / left));
            var rightScale = (uint)Math.Max(1, Math.Round((1 - leftShare) * SIDES_RESOLUTION / right));

            for (var i = 0; i < weights.Length; i++)
                weights[i] *= i < split ? leftScale : rightScale;

            return weights;
        }

        private void EndDrag()
        {
            if (_dragIndex < 0)
                return;

            _dragIndex = -1;

            // Moved weights come back as the smallest numbers with the same odds; a click without a move
            // only takes out what it multiplied in, so the weights typed by hand keep their numbers.
            if (_dragMoved)
                ReduceWeights(0);
            else if (_dragScale > 1)
                ReduceWeights(_dragScale);

            Undo.CollapseUndoOperations(_undoGroup);
            Refresh();
        }

        #endregion

        #region Typed Share

        private void BeginPercentEdit(int index)
        {
            EndPercentEdit(true);

            var elements = _serializedObject.FindProperty(_elementsPath);
            if (elements == null || elements.arraySize < 2)
                return;

            var weights = ReadWeights(elements);
            var total = Sum(weights, 0, weights.Length);
            var segment = _segments[index].layout;
            var barWidth = _bar.contentRect.width;
            var width = Mathf.Min(PERCENT_FIELD_WIDTH, barWidth);

            _percentCancelled = false;
            _percentField = new FloatField {
                value = (float)Math.Round(100.0 * weights[index] / total, 1),
                style = {
                    position = Position.Absolute,
                    top = 0,
                    bottom = 0,
                    width = width,
                    left = Mathf.Clamp(segment.x + (segment.width - width) / 2f, 0, barWidth - width),
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

                EndPercentEdit(!_percentCancelled, index);
            });

            _handleLayer.Add(field);
            field.schedule.Execute(() => field.Focus());
        }

        private void EndPercentEdit(bool apply, int index = -1)
        {
            if (_percentField == null)
                return;

            var field = _percentField;
            _percentField = null;
            field.RemoveFromHierarchy();

            if (apply && index >= 0)
                SetShare(index, field.value);
        }

        /// <summary>Gives the entry this share of the odds; the other entries keep their proportions.</summary>
        private void SetShare(int index, float percent)
        {
            _serializedObject.Update();
            var elements = _serializedObject.FindProperty(_elementsPath);
            if (elements == null || index >= elements.arraySize || elements.arraySize < 2)
                return;

            percent = Mathf.Clamp(percent, MIN_PERCENT, MAX_PERCENT);

            var weights = ReadWeights(elements);
            var others = Sum(weights, 0, weights.Length) - weights[index];

            if (others == 0)
            {
                for (var i = 0; i < weights.Length; i++)
                    if (i != index) weights[i] = 1;

                others = (uint)(weights.Length - 1);
            }

            // Enough resolution for a tenth of a percent before the weights are reduced again.
            var scale = others < MIN_PERCENT_TOTAL ? (MIN_PERCENT_TOTAL + others - 1) / others : 1;
            for (var i = 0; i < weights.Length; i++)
                weights[i] *= scale;

            var otherWeight = (double)others * scale;
            weights[index] = (uint)Math.Max(1, Math.Round(percent * otherWeight / (100 - percent)));

            BeginUndo(PERCENT_UNDO_NAME);
            WriteWeights(elements, weights);
            ReduceWeights(0);
            Undo.CollapseUndoOperations(_undoGroup);

            Refresh();
        }

        #endregion

        private void Equalize()
        {
            _serializedObject.Update();
            var elements = _serializedObject.FindProperty(_elementsPath);
            if (elements == null)
                return;

            var weights = new uint[elements.arraySize];
            Array.Fill(weights, 1u);

            BeginUndo(EQUALIZE_UNDO_NAME);
            WriteWeights(elements, weights);
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
        private void ReduceWeights(uint limit)
        {
            _serializedObject.Update();
            var elements = _serializedObject.FindProperty(_elementsPath);
            var weights = ReadWeights(elements);

            var divisor = limit;
            foreach (var weight in weights)
                divisor = Gcd(divisor, weight);

            if (divisor <= 1)
                return;

            for (var i = 0; i < weights.Length; i++)
                weights[i] /= divisor;

            WriteWeights(elements, weights);
        }

        /// <summary>
        /// Divides the weights in [<paramref name="start"/>, <paramref name="end"/>) by their greatest common divisor,
        /// leaving the smallest numbers with the same proportions, and returns their sum.
        /// </summary>
        private static uint ReduceRange(uint[] weights, int start, int end)
        {
            uint divisor = 0;
            for (var i = start; i < end; i++)
                divisor = Gcd(divisor, weights[i]);

            if (divisor > 1)
                for (var i = start; i < end; i++)
                    weights[i] /= divisor;

            return Sum(weights, start, end);
        }

        private static bool SameWeights(SerializedProperty elements, uint[] weights)
        {
            for (var i = 0; i < weights.Length; i++)
                if (elements.GetArrayElementAtIndex(i).FindPropertyRelative(QUANTITY_PROP_NAME).uintValue != weights[i])
                    return false;

            return true;
        }

        private static uint[] ReadWeights(SerializedProperty elements)
        {
            var weights = new uint[elements.arraySize];
            for (var i = 0; i < weights.Length; i++)
                weights[i] = elements.GetArrayElementAtIndex(i).FindPropertyRelative(QUANTITY_PROP_NAME).uintValue;

            return weights;
        }

        private void WriteWeights(SerializedProperty elements, uint[] weights)
        {
            for (var i = 0; i < weights.Length; i++)
                elements.GetArrayElementAtIndex(i).FindPropertyRelative(QUANTITY_PROP_NAME).uintValue = weights[i];

            _serializedObject.ApplyModifiedProperties();
        }

        private static uint Sum(uint[] weights, int start, int end)
        {
            uint total = 0;
            for (var i = start; i < end; i++)
                total += weights[i];

            return total;
        }

        private static uint Gcd(uint a, uint b)
        {
            while (b != 0)
                (a, b) = (b, a % b);

            return a;
        }

        #endregion

        private static string GetItemName(SerializedProperty element)
        {
            if (element.FindPropertyRelative(ITEM_PROP_NAME).objectReferenceValue is not ItemDef item)
                return NO_ITEM_LABEL;

            return string.IsNullOrEmpty(item.Name) ? item.name : item.Name;
        }

        private static Color SegmentColor(int index)
        {
            var hue = index * GOLDEN_RATIO % 1f;
            return Color.HSVToRGB(hue, 0.45f, EditorGUIUtility.isProSkin ? 0.5f : 0.65f);
        }
    }
}
