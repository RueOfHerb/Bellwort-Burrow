using System.Collections.Generic;
using UnityEngine;

namespace BellwortBurrow.Data
{
    /// <summary>
    /// Every item in the game, so a save can store item ids and find the items again (sys-3).
    /// A new Item Definition goes in this list, or saved stacks of it are dropped on load.
    /// </summary>
    [CreateAssetMenu(menuName = "Bellwort Burrow/Items/Item Catalog", fileName = "ItemCatalog")]
    public class ItemCatalog : ScriptableObject
    {
        [SerializeField] List<ItemDefinition> items = new();

        public IReadOnlyList<ItemDefinition> Items => items;

        public ItemDefinition Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var item in items)
            {
                if (item != null && item.Id == id) return item;
            }
            return null;
        }

        internal void ConfigureForTesting(IEnumerable<ItemDefinition> items) => this.items = new List<ItemDefinition>(items);
    }
}
