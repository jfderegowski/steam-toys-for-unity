using UnityEngine;

namespace SteamToys.Runtime.InventorySystem
{
    /// <summary>
    /// An itemdefid taken out of use. Steam cannot delete a definition, so an old or test one stays on Steam for good:
    /// this overwrites it with a hidden <c>item</c> named <see cref="PLACEHOLDER_NAME"/> that has no properties left to
    /// act on. Keeping the asset in the DB also keeps the itemdefid from being given to a new definition. The Type field
    /// of the inspector turns an existing definition into one, whatever its type was. Only the getters are fixed, so
    /// the JSON is that of a placeholder while the serialized fields of <see cref="ItemDef"/> keep their old values.
    /// <para>
    /// Items players already own stay in their inventories, so <see cref="Inventory.Items"/> lists them too; check for
    /// this class on <see cref="InventoryItem.Definition"/> to leave them out of the game.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "New Placeholder", menuName = "SteamToys/Inventory/Item/Placeholder", order = -5)]
    public class PlaceholderItemDef : ItemDef
    {
        /// <summary>The name of every placeholder; Steam rejects a definition without one.</summary>
        public const string PLACEHOLDER_NAME = "Placeholder";

        public override ItemType GetItemType() => ItemType.Item;

        public override string GetName() => PLACEHOLDER_NAME;

        public override void SetName(string value) { }

        public override bool GetHidden() => true;

        public override void SetHidden(bool value) { }

        public override bool GetGameOnly() => true;

        public override void SetGameOnly(bool value) { }

        public override bool SchemaSupportsTags() => false;
        public override bool SchemaSupportsDescription() => false;
        public override bool SchemaSupportsHidden() => true;
        public override bool SchemaSupportsExchange() => false;
        public override bool SchemaSupportsPromo() => false;
        public override bool SchemaSupportsDropStartTime() => false;
        public override bool SchemaSupportsGrantedManually() => false;
        public override bool SchemaSupportsDropInterval() => false;

        public override bool SchemaSupportsContent() => false;
        public override bool SchemaSupportsTagGenerators() => false;

        public override bool SchemaSupportsDisplayType() => false;
        public override bool SchemaSupportsIconUrl() => false;
        public override bool SchemaSupportsIconUrlLarge() => false;
        public override bool SchemaSupportsNameColor() => false;
        public override bool SchemaSupportsBackgroundColor() => false;
        public override bool SchemaSupportsStoreTags() => false;
        public override bool SchemaSupportsStoreImages() => false;
        public override bool SchemaSupportsStoreHidden() => false;
        public override bool SchemaSupportsPrice() => false;
        public override bool SchemaSupportsPriceCategory() => false;

        public override bool SchemaSupportsUseBundlePrice() => false;
        public override bool SchemaSupportsPurchaseBundleDiscount() => false;

        public override bool SchemaSupportsTradable() => false;
        public override bool SchemaSupportsMarketable() => false;
        public override bool SchemaSupportsGameOnly() => true;
        public override bool SchemaSupportsAutoStack() => false;
        public override bool SchemaSupportsPurchaseLimit() => false;
        public override bool SchemaSupportsContainerContentsGenerator() => false;

        public override bool SchemaSupportsDropWindow() => false;
        public override bool SchemaSupportsDropMaxPerWindow() => false;
        public override bool SchemaSupportsDropLimit() => false;
    }
}
