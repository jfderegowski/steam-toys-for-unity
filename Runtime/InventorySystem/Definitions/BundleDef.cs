namespace SteamToys.Runtime.InventorySystem
{
    /// <summary>
    /// ItemDef of type <c>bundle</c>: expands into all of its <see cref="ItemDef.Content"/> when granted or bought.
    /// Unlike a generator it does not pass tags on to the items it creates, so it has no tags of its own; its
    /// <see cref="ItemDef.TagGenerators"/> do apply to them. It is sold only with a price: the sum of its contents, less
    /// the <see cref="ItemDef.PurchaseBundleDiscount"/>, or its own with <see cref="ItemDef.UseBundlePrice"/>.
    /// </summary>
    public class BundleDef : ItemDef
    {
        public override ItemType GetItemType() => ItemType.Bundle;

        public override bool SchemaSupportsTags() => false;

        public override bool SchemaSupportsDescription() => true;
        public override bool SchemaSupportsHidden() => true;
        public override bool SchemaSupportsExchange() => true;
        public override bool SchemaSupportsPromo() => true;
        public override bool SchemaSupportsDropStartTime() => true;
        public override bool SchemaSupportsGrantedManually() => true;
        public override bool SchemaSupportsDropInterval() => true;

        public override bool SchemaSupportsContent() => true;
        public override bool SchemaSupportsTagGenerators() => true;

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

        public override bool SchemaSupportsUseBundlePrice() => true;
        public override bool SchemaSupportsPurchaseBundleDiscount() => true;

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
}
