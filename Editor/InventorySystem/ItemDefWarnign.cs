using System;
using SteamToys.Runtime.InventorySystem;
using UnityEditor;

namespace SteamToys.Editor.InventorySystem
{
    /// <summary>
    /// A setting of an <see cref="ItemDef"/> that Steam would reject or silently ignore: the serialized field that
    /// needs fixing, the message, and optionally how to fix it, which the inspector offers as a Fix button.
    /// </summary>
    public class ItemDefWarnign : Warning
    {
        public ItemDef ItemDef => _itemDef;
        public string Field => _field;

        private const string FIX_UNDO_NAME = "Fix Item Definition";

        private readonly ItemDef _itemDef;
        private readonly string _field;

        /// <param name="itemDef">The definition the warning is about.</param>
        /// <param name="field">The name of the serialized field that needs fixing; the inspector frames it with the warning.</param>
        /// <param name="message">The message.</param>
        /// <param name="fix">Changes <paramref name="itemDef"/> so the warning goes away; null when it needs a person to decide.</param>
        public ItemDefWarnign(ItemDef itemDef, string field, string message, Action fix = null) : base(message, fix)
        {
            _itemDef = itemDef;
            _field = field;
        }

        /// <summary><paramref name="warning"/>, about <paramref name="field"/> of <paramref name="itemDef"/>.</summary>
        public ItemDefWarnign(ItemDef itemDef, string field, Warning warning) : base(warning)
        {
            _itemDef = itemDef;
            _field = field;
        }

        /// <summary>Runs the fix as one undo step and marks the definition dirty, so it is saved.</summary>
        internal override void Fix()
        {
            if (!CanFix || !_itemDef)
                return;

            Undo.RecordObject(_itemDef, FIX_UNDO_NAME);
            base.Fix();
            EditorUtility.SetDirty(_itemDef);
        }
    }
}
