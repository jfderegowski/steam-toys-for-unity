using UnityEngine;

namespace SteamToys.Runtime.InventorySystem
{
    /// <summary>
    /// ItemDef of type <c>generator</c> with tags of <typeparamref name="TTags"/>, copied to the item it creates: grants
    /// one entry of its <see cref="ItemDef.Content"/>, picked by weight. Never sits in an inventory itself and Steam
    /// does not price it; sell a container <see cref="Item"/> and open it with this generator's exchange instead.
    /// </summary>
    public class Generator<TTags> : ItemDef<TTags> where TTags : Tags, new()
    {
        public override ItemType GetItemType() => ItemType.Generator;

        public override bool SchemaSupportsDescription() => true;
        public override bool SchemaSupportsHidden() => true;
        public override bool SchemaSupportsExchange() => true;
        public override bool SchemaSupportsPromo() => true;
        public override bool SchemaSupportsDropStartTime() => true;
        public override bool SchemaSupportsGrantedManually() => true;
        public override bool SchemaSupportsDropInterval() => true;

        public override bool SchemaSupportsContent() => true;
        public override bool SchemaSupportsTagGenerators() => true;

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
        public override bool SchemaSupportsGameOnly() => false;
        public override bool SchemaSupportsAutoStack() => false;
        public override bool SchemaSupportsPurchaseLimit() => false;
        public override bool SchemaSupportsContainerContentsGenerator() => false;

        public override bool SchemaSupportsDropWindow() => false;
        public override bool SchemaSupportsDropMaxPerWindow() => false;
        public override bool SchemaSupportsDropLimit() => false;
    }

    /// <summary>ItemDef of type <c>generator</c> with tags of any category.</summary>
    [CreateAssetMenu(fileName = "New Generator", menuName = "SteamToys/Inventory/Generator", order = 3)]
    public class Generator : Generator<DefaultTags> { }
}
