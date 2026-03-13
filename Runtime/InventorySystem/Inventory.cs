using Newtonsoft.Json;
using UnityEngine;

namespace SteamToys.Runtime.InventorySystem
{
    [CreateAssetMenu(fileName = "New Inventory", menuName = "SteamToys/Inventory/Inventory", order = 0)]
    public class Inventory : ScriptableObject
    {
        #region Properties

        [JsonProperty("appid")]
        public uint AppId
        {
            get => GetAppId();
            set => SetAppId(value);
        }

        [JsonProperty("items")]
        public Item[] Items
        {
            get => GetItems();
            set => SetItems(value);
        }

        #endregion

        #region Private Fields
        
        private uint _appId;
        private Item[] _items;

        #endregion

        #region Getters and Setters

        public virtual uint GetAppId() => _appId;
        
        public virtual void SetAppId(uint value) => _appId = value;
        
        public virtual Item[] GetItems() => _items;
        
        public virtual void SetItems(Item[] value) => _items = value;
        
        #endregion
    }
}