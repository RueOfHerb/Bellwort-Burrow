using System.Collections.Generic;
using BellwortBurrow.Core;
using BellwortBurrow.Data;
using BellwortBurrow.Systems.Inventory;
using BellwortBurrow.Systems.World;
using NUnit.Framework;
using UnityEngine;

namespace BellwortBurrow.Tests.EditMode
{
    public class ForageableTests
    {
        readonly List<Object> made = new();
        InventoryService inventory;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            inventory = new InventoryService();
            ServiceLocator.Register<IInventoryService>(inventory);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var thing in made)
                if (thing != null) Object.DestroyImmediate(thing);
            made.Clear();
            ServiceLocator.Clear();
        }

        ItemDefinition Item(string id)
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            item.ConfigureForTesting(id);
            made.Add(item);
            return item;
        }

        Forageable Node(ItemDefinition item, Vector2 at, int amount = 1, int respawnDays = 1)
        {
            var go = new GameObject("node");
            go.transform.position = at;
            go.AddComponent<SpriteRenderer>();
            made.Add(go);
            var node = go.AddComponent<Forageable>();
            node.Configure(item, amount, respawnDays);
            Interactables.Add(node);
            return node;
        }

        GameObject Player()
        {
            var go = new GameObject("player");
            made.Add(go);
            return go;
        }

        [Test]
        public void InteractGivesTheItemAndHidesThePiece()
        {
            var node = Node(Item("bellwort"), Vector2.zero, amount: 2);
            var who = Player();

            node.Interact(who);

            Assert.AreEqual(1, inventory.Slots.Count);
            Assert.AreEqual("bellwort", inventory.Slots[0].Item.Id);
            Assert.AreEqual(2, inventory.Slots[0].Quantity);
            Assert.IsTrue(node.Taken);
            Assert.IsFalse(node.CanInteract(who));
            Assert.IsFalse(node.GetComponent<SpriteRenderer>().enabled);
        }

        [Test]
        public void ABonusItemComesAlongWithTheItem()
        {
            var node = Node(Item("bellwort_flower"), Vector2.zero);
            node.Configure(node.Item, 1, 1, Item("bellwort_seed"), 1);

            node.Interact(Player());

            Assert.AreEqual(2, inventory.Slots.Count);
            Assert.AreEqual("bellwort_flower", inventory.Slots[0].Item.Id);
            Assert.AreEqual("bellwort_seed", inventory.Slots[1].Item.Id);
        }

        [Test]
        public void ItComesBackAfterItsRespawnDays()
        {
            var node = Node(Item("stone"), Vector2.zero, respawnDays: 2);
            var who = Player();
            node.Interact(who);

            node.DayPassed();
            Assert.IsTrue(node.Taken, "one day isn't enough");
            node.DayPassed();

            Assert.IsFalse(node.Taken);
            Assert.IsTrue(node.CanInteract(who));
            Assert.IsTrue(node.GetComponent<SpriteRenderer>().enabled);
        }

        [Test]
        public void NothingHappensWithoutAnItem()
        {
            var node = Node(null, Vector2.zero);
            var who = Player();

            node.Interact(who);

            Assert.AreEqual(0, inventory.Slots.Count);
            Assert.IsFalse(node.Taken);
        }

        [Test]
        public void NearestPicksTheClosestOneInReachThatWillTakeIt()
        {
            var who = Player();
            var far = Node(Item("a"), new Vector2(5f, 0f));
            var near = Node(Item("b"), new Vector2(0.8f, 0f));
            var nearer = Node(Item("c"), new Vector2(0.4f, 0f));
            nearer.Interact(who); // already picked, so it doesn't count

            var found = Interactables.Nearest(Vector2.zero, 1.25f, who);

            Assert.AreSame(near, found);
            Assert.IsNull(Interactables.Nearest(new Vector2(20f, 0f), 1.25f, who));
            Assert.IsNotNull(far);
        }
    }
}
