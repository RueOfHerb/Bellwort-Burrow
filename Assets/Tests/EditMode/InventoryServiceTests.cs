using NUnit.Framework;
using UnityEngine;
using BellwortBurrow.Data;
using BellwortBurrow.Systems.Inventory;

namespace BellwortBurrow.Tests.EditMode
{
    public class InventoryServiceTests
    {
        [Test]
        public void TryAdd_ExceedsStackSize_SplitsAcrossMultipleSlots()
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            var inventory = new InventoryService();
            int changedCount = 0;
            inventory.OnInventoryChanged += () => changedCount++;

            bool added = inventory.TryAdd(item, 150);

            Assert.IsTrue(added);
            Assert.AreEqual(1, changedCount);
            Assert.AreEqual(2, inventory.Slots.Count);
            Assert.AreEqual(99, inventory.Slots[0].Quantity);
            Assert.AreEqual(51, inventory.Slots[1].Quantity);
        }

        [Test]
        public void TryRemove_InsufficientQuantity_ReturnsFalseAndLeavesSlotsUnchanged()
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            var inventory = new InventoryService();
            inventory.TryAdd(item, 10);

            bool removed = inventory.TryRemove(item, 20);

            Assert.IsFalse(removed);
            Assert.AreEqual(1, inventory.Slots.Count);
            Assert.AreEqual(10, inventory.Slots[0].Quantity);
        }
    }
}
