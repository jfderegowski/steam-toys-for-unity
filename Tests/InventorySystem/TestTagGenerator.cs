using SteamToys.Runtime.InventorySystem;
using UnityEngine;

namespace SteamToys.Tests.Tests
{
    [CreateAssetMenu(fileName = "TestTagGenerator", menuName = "SteamToys/Inventory/Test/TestTagGenerator")]
    public class TestTagGenerator : TagGenerator<Rarity> { }
}