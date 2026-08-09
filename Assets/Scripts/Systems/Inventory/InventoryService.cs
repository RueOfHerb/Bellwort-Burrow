using System;
using System.Collections.Generic;
using BellwortBurrow.Core;
using BellwortBurrow.Data;

namespace BellwortBurrow.Systems.Inventory
{
    public class InventoryService : IInventoryService, ISaveable
    {
        readonly List<InventorySlot> slots = new();
        readonly int capacity;

        public InventoryService(int capacity = 24)
        {
            this.capacity = capacity;
        }

        public string SaveKey => "Inventory";
        public IReadOnlyList<InventorySlot> Slots => slots;
        public event Action OnInventoryChanged;

        public bool TryAdd(ItemDefinition item, int quantity)
        {
            if (item == null || quantity <= 0) return false;

            for (int i = 0; i < slots.Count && quantity > 0; i++)
            {
                if (slots[i].Item != item || slots[i].Quantity >= item.MaxStackSize) continue;

                int toAdd = Math.Min(item.MaxStackSize - slots[i].Quantity, quantity);
                slots[i] = new InventorySlot(item, slots[i].Quantity + toAdd);
                quantity -= toAdd;
            }

            while (quantity > 0 && slots.Count < capacity)
            {
                int toAdd = Math.Min(item.MaxStackSize, quantity);
                slots.Add(new InventorySlot(item, toAdd));
                quantity -= toAdd;
            }

            OnInventoryChanged?.Invoke();
            return quantity == 0;
        }

        public bool TryRemove(ItemDefinition item, int quantity)
        {
            if (item == null || quantity <= 0) return false;

            int available = 0;
            foreach (var slot in slots)
            {
                if (slot.Item == item) available += slot.Quantity;
            }
            if (available < quantity) return false;

            for (int i = slots.Count - 1; i >= 0 && quantity > 0; i--)
            {
                if (slots[i].Item != item) continue;

                int toRemove = Math.Min(slots[i].Quantity, quantity);
                int remaining = slots[i].Quantity - toRemove;
                quantity -= toRemove;

                if (remaining > 0)
                    slots[i] = new InventorySlot(item, remaining);
                else
                    slots.RemoveAt(i);
            }

            OnInventoryChanged?.Invoke();
            return true;
        }

        public object CaptureState() => slots.ToArray();

        public void RestoreState(object state)
        {
            slots.Clear();
            if (state is InventorySlot[] savedSlots)
                slots.AddRange(savedSlots);
            OnInventoryChanged?.Invoke();
        }
    }
}
