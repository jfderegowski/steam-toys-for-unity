namespace SteamToys.Runtime.InventorySystem
{
    /// <summary>
    /// ItemDef of type <c>playtimegenerator</c> with tags of <typeparamref name="TTags"/>: a generator the application
    /// can also drop through <c>ISteamInventory::TriggerItemDrop</c>, paced by the drop settings and the
    /// <see cref="ItemDef.DropInterval"/>. Any drop setting left unset falls back to the application level one. Its
    /// exchange works as a plain generator's: the materials buy one pick of its content, apart from the drops. The
    /// Steam client never gets its definition.
    /// </summary>
    public class PlaytimeGeneratorDef<TTags> : ItemDef<TTags> where TTags : Tags, new()
    {
        public override ItemType GetItemType() => ItemType.PlaytimeGenerator;

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

        public override bool SchemaSupportsDropWindow() => true;
        public override bool SchemaSupportsDropMaxPerWindow() => true;
        public override bool SchemaSupportsDropLimit() => true;
    }
}
