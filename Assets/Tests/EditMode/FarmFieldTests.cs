using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;
using BellwortBurrow.Core;
using BellwortBurrow.Data;
using BellwortBurrow.Data.Professions.Botany;
using BellwortBurrow.Systems.Calendar;
using BellwortBurrow.Systems.Inventory;
using BellwortBurrow.Systems.Professions;
using BellwortBurrow.Systems.Professions.Botany;
using BellwortBurrow.Systems.World;

namespace BellwortBurrow.Tests.EditMode
{
    public class FarmFieldTests
    {
        readonly List<Object> made = new();
        Tilemap tillable, farmland, watered, crops;
        Tile dirt, tilled, wet;
        Tile[] stages;
        ItemDefinition seed, flower;
        BotanyCropDefinition crop;
        TimeService time;
        InventoryService inventory;
        BotanyPlotService plots;
        FarmField farm;
        GameObject player;
        readonly Vector2 at = new(0.5f, 0.5f);   // the middle of cell (0, 0)

        T Make<T>() where T : ScriptableObject
        {
            var thing = ScriptableObject.CreateInstance<T>();
            made.Add(thing);
            return thing;
        }

        Tilemap Layer(GameObject grid, string name)
        {
            var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
            go.transform.SetParent(grid.transform);
            return go.GetComponent<Tilemap>();
        }

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            var grid = new GameObject("Grid", typeof(Grid));
            made.Add(grid);
            tillable = Layer(grid, "Tillable");
            farmland = Layer(grid, "Farmland");
            watered = Layer(grid, "Watered");
            crops = Layer(grid, "Crops");
            dirt = Make<Tile>(); tilled = Make<Tile>(); wet = Make<Tile>();
            stages = new[] { Make<Tile>(), Make<Tile>(), Make<Tile>() };
            for (int y = -1; y <= 1; y++)            // a 3 by 3 patch, so (0, 0) is inside its edge
                for (int x = -1; x <= 1; x++) tillable.SetTile(new Vector3Int(x, y, 0), dirt);

            var profession = Make<ProfessionDefinition>();
            profession.ConfigureForTesting(AnimationCurve.Constant(0, 100, 1000));
            seed = Make<ItemDefinition>(); seed.ConfigureForTesting("bellwort_seed");
            flower = Make<ItemDefinition>(); flower.ConfigureForTesting("bellwort_flower");
            crop = Make<BotanyCropDefinition>();
            crop.ConfigureForTesting(profession, seed, flower, 3, 1, 5, "bellwort");

            var calendar = Make<CalendarConfig>();
            calendar.ConfigureForTesting(daysPerSeason: 28, realSecondsPerInGameMinute: 0.01f, minutesPerHour: 60, hoursPerDay: 24);
            time = new TimeService(calendar);
            inventory = new InventoryService();
            plots = new BotanyPlotService(new ProfessionService(), time);
            ServiceLocator.Register<IInventoryService>(inventory);
            ServiceLocator.Register(plots);

            var farmObject = new GameObject("Farm");
            made.Add(farmObject);
            farm = farmObject.AddComponent<FarmField>();
            farm.Configure(tillable, dirt, farmland, tilled, watered, wet, crops,
                new[] { new FarmField.CropLook { crop = crop, stages = stages } });
            player = new GameObject("player");
            made.Add(player);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var thing in made) if (thing != null) Object.DestroyImmediate(thing);
            made.Clear();
            ServiceLocator.Clear();
        }

        void LiveADay()
        {
            time.Tick(8.45f);
            time.StartNextDay();
            farm.Redraw();
        }

        [Test]
        public void ETillsThenPlantsASeedThenWaters()
        {
            inventory.TryAdd(seed, 2);

            Assert.IsTrue(farm.TryUse(at, player)); Assert.AreEqual("till", farm.LastAction);
            Assert.AreSame(tilled, farmland.GetTile(Vector3Int.zero));

            Assert.IsTrue(farm.TryUse(at, player)); Assert.AreEqual("plant", farm.LastAction);
            Assert.AreSame(stages[0], crops.GetTile(Vector3Int.zero));
            Assert.AreEqual(1, inventory.Slots[0].Quantity, "a seed used up");

            Assert.IsTrue(farm.TryUse(at, player)); Assert.AreEqual("water", farm.LastAction);
            Assert.AreSame(wet, watered.GetTile(Vector3Int.zero));
            Assert.IsFalse(farm.TryUse(at, player), "nothing more to do today");
        }

        [Test]
        public void OnlyTheTillableDirtTills()
        {
            Assert.IsFalse(farm.TryUse(new Vector2(3.5f, 0.5f), player), "bare cell");
            tillable.SetTile(new Vector3Int(3, 0, 0), Make<Tile>());
            Assert.IsFalse(farm.TryUse(new Vector2(3.5f, 0.5f), player), "some other tile");
        }

        [Test]
        public void WithoutASeedETilledGroundWaters()
        {
            farm.TryUse(at, player);
            Assert.IsTrue(farm.TryUse(at, player));
            Assert.AreEqual("water", farm.LastAction);
            Assert.IsFalse(plots.HasCrop(Vector3Int.zero));
        }

        [Test]
        public void ARipeCropHarvestsIntoTheInventory_AndTheGroundStaysTilled()
        {
            inventory.TryAdd(seed, 1);
            farm.TryUse(at, player);
            farm.TryUse(at, player);
            for (int i = 0; i < 2; i++) { farm.TryUse(at, player); LiveADay(); }
            Assert.AreSame(stages[2], crops.GetTile(Vector3Int.zero));
            Assert.IsNull(watered.GetTile(Vector3Int.zero), "dried out overnight");

            Assert.IsTrue(farm.TryUse(at, player));
            Assert.AreEqual("harvest", farm.LastAction);
            Assert.AreSame(flower, inventory.Slots[0].Item);
            Assert.IsNull(crops.GetTile(Vector3Int.zero));
            Assert.AreSame(tilled, farmland.GetTile(Vector3Int.zero));
        }

        [Test]
        public void ActionAtSaysWhatEWouldDo_WithoutDoingIt()
        {
            Assert.AreEqual("till", farm.ActionAt(at));
            Assert.IsFalse(plots.IsTilled(Vector3Int.zero), "asking changes nothing");
            Assert.AreEqual("", farm.ActionAt(new Vector2(3.5f, 0.5f)), "bare grass");
            farm.TryUse(at, player);
            Assert.AreEqual("water", farm.ActionAt(at), "no seed in the bag");
            inventory.TryAdd(seed, 1);
            Assert.AreEqual("plant", farm.ActionAt(at));
            farm.TryUse(at, player);
            Assert.AreEqual("water", farm.ActionAt(at));
            farm.TryUse(at, player);
            Assert.AreEqual("", farm.ActionAt(at), "watered, still growing: nothing to do");
            Assert.IsNull(FarmField.TargetAt(new Vector2(3.5f, 0.5f), out var none), "no farm would do anything on bare grass");
            Assert.AreEqual("", none);
            var frame = farm.CellBounds(at);
            Assert.AreEqual(new Vector3(0.5f, 0.5f, 0f), frame.center);
            Assert.AreEqual(Vector3.one, new Vector3(frame.size.x, frame.size.y, 1f));
        }

        [Test]
        public void ThePatchsEdgeCantBeTilledOrWatered()
        {
            var edge = new Vector2(1.5f, 0.5f);
            Assert.IsTrue(farm.IsEdge(new Vector3Int(1, 0, 0)));
            Assert.AreEqual("", farm.ActionAt(edge));
            Assert.IsFalse(farm.TryUse(edge, player), "no tilling on the edge");
            plots.TryTill(new Vector3Int(1, 0, 0));   // tilled before the rule, in an older save
            Assert.AreEqual("", farm.ActionAt(edge));
            Assert.IsFalse(farm.TryUse(edge, player), "no watering on the edge");
            Assert.IsFalse(plots.IsWatered(new Vector3Int(1, 0, 0)));
            Assert.AreEqual("till", farm.ActionAt(at), "the middle still tills");
        }
    }
}
