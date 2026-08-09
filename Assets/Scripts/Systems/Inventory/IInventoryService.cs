using System;
using System.Collections.Generic;
using BellwortBurrow.Data;

namespace BellwortBurrow.Systems.Inventory
{
    public readonly struct InventorySlot
    {
        public readonly ItemDefinition Item;
        public readonly int Quantity;

        public InventorySlot(ItemDefinition item, int quantity)
        {
            Item = item;
            Quantity = quantity;
        }
    }

    public interface IInventoryService
    {
        IReadOnlyList<InventorySlot> Slots { get; }

        bool TryAdd(ItemDefinition item, int quantity);
        bool TryRemove(ItemDefinition item, int quantity);

        event Action OnInventoryChanged;
    }
}
