using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SteamToys.Runtime.InventorySystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SteamToys.Editor.InventorySystem
{
    [CustomEditor(typeof(ItemDef), true), CanEditMultipleObjects]
    public class ItemDefEditor : UnityEditor.Editor
    {
        #region Constants

        private const string SCRIPT_PROPERTY = "m_Script";
        private const char SUBMENU_SEPARATOR = '/';

        private const string TYPE_TOOLTIP =
            "The Steam type and class of the definition. Choosing another class changes it; the values both classes have are kept.";

        #endregion

        #region Fields

        private static readonly Dictionary<Type, MonoScript> Scripts = new();

        #endregion

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            var typeField = CreateTypeField();

            if (typeField != null)
                root.Add(typeField);

            root.Add(new ItemDefElement(serializedObject, GetWarnings));

            return root;
        }

        #region Warnings

        private (string Field, Func<ItemDef, IEnumerable<Warning>> Get)[] _fieldWarnings;

        /// <summary>
        /// The warnings of an editor of a subclass for <paramref name="item"/>; the built-in ones are collected
        /// anyway. They are shown in the inspector, a warning with a fix with a Fix button, and in the list of the
        /// Steam Items DB. By default it returns those of each field the type of <paramref name="item"/> uses, so
        /// overriding one of those is usually enough; override this for warnings that belong to no single field.
        /// </summary>
        protected internal virtual IEnumerable<ItemDefWarnign> GetWarnings(ItemDef item) =>
            ItemDefElement.GetFieldWarnings(item, FieldWarnings);

        /// <summary>Each serialized field of <see cref="ItemDef"/> with the method returning its warnings.</summary>
        private (string Field, Func<ItemDef, IEnumerable<Warning>> Get)[] FieldWarnings =>
            _fieldWarnings ??= new (string, Func<ItemDef, IEnumerable<Warning>>)[] {
                (ItemDefElement.ITEM_DEF_ID, GetItemDefIdWarnings),
                (ItemDefElement.NAME, GetNameWarnings),
                (ItemDefElement.DESCRIPTION, GetDescriptionWarnings),
                (ItemDefElement.TAGS, GetTagsWarnings),
                (ItemDefElement.HIDDEN, GetHiddenWarnings),
                (ItemDefElement.DISPLAY_TYPE, GetDisplayTypeWarnings),
                (ItemDefElement.ICON_URL, GetIconUrlWarnings),
                (ItemDefElement.ICON_URL_LARGE, GetIconUrlLargeWarnings),
                (ItemDefElement.ICON, GetIconWarnings),
                (ItemDefElement.ICON_LARGE, GetIconLargeWarnings),
                (ItemDefElement.NAME_COLOR, GetNameColorWarnings),
                (ItemDefElement.BACKGROUND_COLOR, GetBackgroundColorWarnings),
                (ItemDefElement.CONTENT, GetContentWarnings),
                (ItemDefElement.TAG_GENERATORS, GetTagGeneratorsWarnings),
                (ItemDefElement.PRICE, GetPriceWarnings),
                (ItemDefElement.PRICE_CATEGORY, GetPriceCategoryWarnings),
                (ItemDefElement.USE_BUNDLE_PRICE, GetUseBundlePriceWarnings),
                (ItemDefElement.PURCHASE_BUNDLE_DISCOUNT, GetPurchaseBundleDiscountWarnings),
                (ItemDefElement.STORE_TAGS, GetStoreTagsWarnings),
                (ItemDefElement.STORE_IMAGES, GetStoreImagesWarnings),
                (ItemDefElement.STORE_HIDDEN, GetStoreHiddenWarnings),
                (ItemDefElement.TRADABLE, GetTradableWarnings),
                (ItemDefElement.MARKETABLE, GetMarketableWarnings),
                (ItemDefElement.GAME_ONLY, GetGameOnlyWarnings),
                (ItemDefElement.AUTO_STACK, GetAutoStackWarnings),
                (ItemDefElement.PURCHASE_LIMIT, GetPurchaseLimitWarnings),
                (ItemDefElement.CONTAINER_CONTENTS_GENERATOR, GetContainerContentsGeneratorWarnings),
                (ItemDefElement.EXCHANGE, GetExchangeWarnings),
                (ItemDefElement.PROMO, GetPromoWarnings),
                (ItemDefElement.DROP_START_TIME, GetDropStartTimeWarnings),
                (ItemDefElement.GRANTED_MANUALLY, GetGrantedManuallyWarnings),
                (ItemDefElement.DROP_INTERVAL, GetDropIntervalWarnings),
                (ItemDefElement.DROP_WINDOW, GetDropWindowWarnings),
                (ItemDefElement.DROP_MAX_PER_WINDOW, GetDropMaxPerWindowWarnings),
                (ItemDefElement.DROP_LIMIT, GetDropLimitWarnings),
            };

        // One per serialized field of ItemDef, each called only when the type of the item uses the field. Each warning
        // is about that field, so the inspector frames it there.

        protected virtual IEnumerable<Warning> GetItemDefIdWarnings(ItemDef item) => Enumerable.Empty<Warning>();
        protected virtual IEnumerable<Warning> GetNameWarnings(ItemDef item) => Enumerable.Empty<Warning>();
        protected virtual IEnumerable<Warning> GetDescriptionWarnings(ItemDef item) => Enumerable.Empty<Warning>();
        protected virtual IEnumerable<Warning> GetTagsWarnings(ItemDef item) => Enumerable.Empty<Warning>();
        protected virtual IEnumerable<Warning> GetHiddenWarnings(ItemDef item) => Enumerable.Empty<Warning>();
        protected virtual IEnumerable<Warning> GetDisplayTypeWarnings(ItemDef item) => Enumerable.Empty<Warning>();
        protected virtual IEnumerable<Warning> GetIconUrlWarnings(ItemDef item) => Enumerable.Empty<Warning>();
        protected virtual IEnumerable<Warning> GetIconUrlLargeWarnings(ItemDef item) => Enumerable.Empty<Warning>();
        protected virtual IEnumerable<Warning> GetIconWarnings(ItemDef item) => Enumerable.Empty<Warning>();
        protected virtual IEnumerable<Warning> GetIconLargeWarnings(ItemDef item) => Enumerable.Empty<Warning>();
        protected virtual IEnumerable<Warning> GetNameColorWarnings(ItemDef item) => Enumerable.Empty<Warning>();
        protected virtual IEnumerable<Warning> GetBackgroundColorWarnings(ItemDef item) => Enumerable.Empty<Warning>();
        protected virtual IEnumerable<Warning> GetContentWarnings(ItemDef item) => Enumerable.Empty<Warning>();
        protected virtual IEnumerable<Warning> GetTagGeneratorsWarnings(ItemDef item) => Enumerable.Empty<Warning>();
        protected virtual IEnumerable<Warning> GetPriceWarnings(ItemDef item) => Enumerable.Empty<Warning>();
        protected virtual IEnumerable<Warning> GetPriceCategoryWarnings(ItemDef item) => Enumerable.Empty<Warning>();
        protected virtual IEnumerable<Warning> GetUseBundlePriceWarnings(ItemDef item) => Enumerable.Empty<Warning>();
        protected virtual IEnumerable<Warning> GetPurchaseBundleDiscountWarnings(ItemDef item) => Enumerable.Empty<Warning>();
        protected virtual IEnumerable<Warning> GetStoreTagsWarnings(ItemDef item) => Enumerable.Empty<Warning>();
        protected virtual IEnumerable<Warning> GetStoreImagesWarnings(ItemDef item) => Enumerable.Empty<Warning>();
        protected virtual IEnumerable<Warning> GetStoreHiddenWarnings(ItemDef item) => Enumerable.Empty<Warning>();
        protected virtual IEnumerable<Warning> GetTradableWarnings(ItemDef item) => Enumerable.Empty<Warning>();
        protected virtual IEnumerable<Warning> GetMarketableWarnings(ItemDef item) => Enumerable.Empty<Warning>();
        protected virtual IEnumerable<Warning> GetGameOnlyWarnings(ItemDef item) => Enumerable.Empty<Warning>();
        protected virtual IEnumerable<Warning> GetAutoStackWarnings(ItemDef item) => Enumerable.Empty<Warning>();
        protected virtual IEnumerable<Warning> GetPurchaseLimitWarnings(ItemDef item) => Enumerable.Empty<Warning>();
        protected virtual IEnumerable<Warning> GetContainerContentsGeneratorWarnings(ItemDef item) => Enumerable.Empty<Warning>();
        protected virtual IEnumerable<Warning> GetExchangeWarnings(ItemDef item) => Enumerable.Empty<Warning>();
        protected virtual IEnumerable<Warning> GetPromoWarnings(ItemDef item) => Enumerable.Empty<Warning>();
        protected virtual IEnumerable<Warning> GetDropStartTimeWarnings(ItemDef item) => Enumerable.Empty<Warning>();
        protected virtual IEnumerable<Warning> GetGrantedManuallyWarnings(ItemDef item) => Enumerable.Empty<Warning>();
        protected virtual IEnumerable<Warning> GetDropIntervalWarnings(ItemDef item) => Enumerable.Empty<Warning>();
        protected virtual IEnumerable<Warning> GetDropWindowWarnings(ItemDef item) => Enumerable.Empty<Warning>();
        protected virtual IEnumerable<Warning> GetDropMaxPerWindowWarnings(ItemDef item) => Enumerable.Empty<Warning>();
        protected virtual IEnumerable<Warning> GetDropLimitWarnings(ItemDef item) => Enumerable.Empty<Warning>();

        #endregion

        #region Type

        /// <summary>
        /// Every class with a <see cref="CreateAssetMenuAttribute"/>, under its Steam type, with the class of the
        /// selected definitions chosen. Null when they are of different classes.
        /// </summary>
        private DropdownField CreateTypeField()
        {
            var current = target.GetType();

            if (targets.Any(item => item.GetType() != current))
                return null;

            var classes = GetClasses(current);
            var labels = GetLabels(classes);
            var currentIndex = classes.IndexOf(current);

            var field = new DropdownField(labels, currentIndex) {
                tooltip = TYPE_TOOLTIP
            };

            field.RegisterValueChangedCallback(change => {
                var index = labels.IndexOf(change.newValue);

                if (index < 0 || index == currentIndex)
                    return;

                if (ConfirmChange(current, classes[index]))
                    ChangeClass(classes[index]);
                else
                    field.SetValueWithoutNotify(change.previousValue);
            });

            return field;
        }

        /// <summary>
        /// The classes inheriting <see cref="ItemDef"/> that Unity can make a ScriptableObject of and that have a
        /// <see cref="CreateAssetMenuAttribute"/>, in the order of the Steam types, then by the order of the attribute,
        /// then by name. <paramref name="current"/> is always listed, last when it has no attribute, so a definition of
        /// a class without the attribute still shows what it is.
        /// </summary>
        private static List<Type> GetClasses(Type current) =>
            Enum.GetValues(typeof(ItemType)).Cast<ItemType>()
                .SelectMany(type => ItemDefsFromSteam.GetConcreteTypes(type)
                    .Where(candidate => GetScript(candidate) && (candidate == current || GetMenuName(candidate) != null))
                    .OrderBy(candidate => candidate.GetCustomAttribute<CreateAssetMenuAttribute>(false)?.order ?? int.MaxValue)
                    .ThenBy(GetLabel))
                .ToList();

        /// <summary>
        /// A label for each of <paramref name="classes"/>. The value of the field says which one is chosen, so classes
        /// with the same menu name under one type also get the name of their class.
        /// </summary>
        private static List<string> GetLabels(IReadOnlyCollection<Type> classes)
        {
            var labels = classes.Select(GetLabel).ToList();

            return classes
                .Select(type => (type, label: GetLabel(type)))
                .Select(entry => labels.Count(label => label == entry.label) > 1
                    ? $"{entry.label} ({entry.type.Name})"
                    : entry.label)
                .ToList();
        }

        /// <summary>The Steam type with the menu name of the class in its submenu, e.g. <c>Generator/Generator</c>.</summary>
        private static string GetLabel(Type type) =>
            GetTypeName(type) + SUBMENU_SEPARATOR + (GetMenuName(type) ?? ObjectNames.NicifyVariableName(type.Name));

        /// <summary>
        /// The last part of the <see cref="CreateAssetMenuAttribute.menuName"/> of <paramref name="type"/>, or its class
        /// name when the attribute names no menu, as Unity does. Null without the attribute.
        /// </summary>
        private static string GetMenuName(Type type)
        {
            var attribute = type.GetCustomAttribute<CreateAssetMenuAttribute>(false);

            if (attribute == null)
                return null;

            var menuName = attribute.menuName?.Split(SUBMENU_SEPARATOR, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();

            return string.IsNullOrWhiteSpace(menuName) ? ObjectNames.NicifyVariableName(type.Name) : menuName.Trim();
        }

        private static string GetTypeName(Type type) =>
            ObjectNames.NicifyVariableName(ItemDefsFromSteam.GetItemType(type).ToString());

        private bool ConfirmChange(Type from, Type to)
        {
            var subject = targets.Length == 1 ? target.name : $"the {targets.Length} selected definitions";
            var toType = GetTypeName(to);
            var fromTags = ItemDefsFromSteam.GetTagsType(from);
            var toTags = ItemDefsFromSteam.GetTagsType(to);

            var message = $"Change {subject} from {ObjectNames.NicifyVariableName(from.Name)} to " +
                          $"{ObjectNames.NicifyVariableName(to.Name)}, a {toType}?\n\n" +
                          "The values both classes have are kept, and only those the new type uses are shown and exported.";

            // Tags are a field of ItemDef<TTags>, so only the tags of the same TTags surely carry over.
            if (fromTags != null && toTags == null)
                message += $" The tags are lost: a {toType} has none.";
            else if (fromTags != null && toTags != fromTags)
                message += $" The tags may not carry over: {ObjectNames.NicifyVariableName(to.Name)} keeps tags of " +
                           $"{ObjectNames.NicifyVariableName(toTags.Name)}, not {ObjectNames.NicifyVariableName(fromTags.Name)}.";

            return EditorUtility.DisplayDialog("Change Item Type", message, "Change", "Cancel");
        }

        /// <summary>Swaps the script of the selected definitions; Unity rebuilds the inspector for the new class.</summary>
        private void ChangeClass(Type type)
        {
            serializedObject.FindProperty(SCRIPT_PROPERTY).objectReferenceValue = GetScript(type);
            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>The script of <paramref name="type"/>, or null when its file is named otherwise and Unity cannot make assets of it.</summary>
        private static MonoScript GetScript(Type type)
        {
            if (Scripts.TryGetValue(type, out var script))
                return script;

            var instance = CreateInstance(type);

            try
            {
                script = MonoScript.FromScriptableObject(instance);

                return Scripts[type] = script && script.GetClass() == type ? script : null;
            }
            finally
            {
                DestroyImmediate(instance);
            }
        }

        #endregion
    }
}
