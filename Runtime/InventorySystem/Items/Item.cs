using UnityEngine;

namespace SteamToys.Runtime.InventorySystem
{
    /// <summary>
    /// ItemDef of type <c>item</c> with tags of <typeparamref name="TTags"/>: the only type that actually sits in a
    /// player's inventory. Each one a player owns is an <see cref="InventoryItem"/> of <see cref="Inventory"/>, which
    /// refers to this definition. Subclass it with a <see cref="TagsDef"/> of the game, or use <see cref="Item"/>.
    /// </summary>
    public class Item<TTags> : ItemDef<TTags> where TTags : TagsDef, new()
    {
        public override ItemType GetItemType() => ItemType.Item;

        public override bool SchemaSupportsDescription() => true;
        public override bool SchemaSupportsHidden() => true;
        public override bool SchemaSupportsExchange() => true;
        public override bool SchemaSupportsPromo() => true;
        public override bool SchemaSupportsDropStartTime() => true;
        public override bool SchemaSupportsGrantedManually() => true;
        public override bool SchemaSupportsDropInterval() => true;

        public override bool SchemaSupportsContent() => false;
        public override bool SchemaSupportsTagGenerators() => false;

        public override bool SchemaSupportsDisplayType() => true;
        public override bool SchemaSupportsIconUrl() => true;
        public override bool SchemaSupportsIconUrlLarge() => true;
        public override bool SchemaSupportsNameColor() => true;
        public override bool SchemaSupportsBackgroundColor() => true;
        public override bool SchemaSupportsStoreTags() => true;
        public override bool SchemaSupportsStoreImages() => true;
        public override bool SchemaSupportsStoreHidden() => true;
        public override bool SchemaSupportsPrice() => true;
        public override bool SchemaSupportsPriceCategory() => true;

        public override bool SchemaSupportsUseBundlePrice() => false;
        public override bool SchemaSupportsPurchaseBundleDiscount() => false;

        public override bool SchemaSupportsTradable() => true;
        public override bool SchemaSupportsMarketable() => true;
        public override bool SchemaSupportsGameOnly() => true;
        public override bool SchemaSupportsAutoStack() => true;
        public override bool SchemaSupportsPurchaseLimit() => true;
        public override bool SchemaSupportsContainerContentsGenerator() => true;

        public override bool SchemaSupportsDropWindow() => false;
        public override bool SchemaSupportsDropMaxPerWindow() => false;
        public override bool SchemaSupportsDropLimit() => false;
    }

    /// <summary>ItemDef of type <c>item</c> with tags of any category.</summary>
    [CreateAssetMenu(fileName = "New Item", menuName = "SteamToys/Inventory/Item", order = 1)]
    public class Item : Item<Tags> { }
}
