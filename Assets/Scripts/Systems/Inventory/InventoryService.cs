using System;
using System.Collections.Generic;
using UnityEngine;
using BellwortBurrow.Core;
using BellwortBurrow.Data;

namespace BellwortBurrow.Systems.Inventory
{
    public class InventoryService : IInventoryService, ISaveable
    {
        [Serializable]
        class SlotState
        {
            public string item;
            public int quantity;
        }

        [Serializable]
        class InventoryState
        {
            public List<SlotState> slots = new();
        }

        readonly List<InventorySlot> slots = new();
        readonly int capacity;
        readonly Func<string, ItemDefinition> findItem;

        /// <param name="findItem">Finds an item by id when a save is loaded (the Item Catalog's Find).</param>
        public InventoryService(int capacity = 24, Func<string, ItemDefinition> findItem = null)
        {
            this.capacity = capacity;
            this.findItem = findItem;
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

        // Saves item ids, never the Item Definitions themselves (sys-3).
        public string CaptureState()
        {
            var state = new InventoryState();
            foreach (var slot in slots)
                state.slots.Add(new SlotState { item = slot.Item != null ? slot.Item.Id : "", quantity = slot.Quantity });
            return JsonUtility.ToJson(state);
        }

        public void RestoreState(string json)
        {
            slots.Clear();
            var state = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<InventoryState>(json);

            foreach (var saved in state?.slots ?? new List<SlotState>())
            {
                if (saved.quantity <= 0 || slots.Count >= capacity) continue;

                var item = findItem?.Invoke(saved.item);
                if (item == null)
                {
                    Debug.LogWarning($"[Inventory] No item '{saved.item}' in the Item Catalog, so a stack of {saved.quantity} was left out of the loaded save.");
                    continue;
                }
                slots.Add(new InventorySlot(item, Math.Min(saved.quantity, item.MaxStackSize)));
            }

            OnInventoryChanged?.Invoke();
        }
    }
}
