using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using BellwortBurrow.Data;
using BellwortBurrow.Data.Professions.Botany;
using BellwortBurrow.Systems.Calendar;
using BellwortBurrow.Systems.Professions;
using BellwortBurrow.Systems.Professions.Botany;

namespace BellwortBurrow.Tests.EditMode
{
    public class BotanyPlotServiceTests
    {
        readonly List<Object> made = new();
        ProfessionDefinition profession;
        ItemDefinition seed, flower;
        BotanyCropDefinition crop;
        TimeService time;
        ProfessionService professions;
        BotanyPlotService plots;
        readonly Vector3Int cell = new(2, -1, 0);

        T Make<T>() where T : ScriptableObject
        {
            var thing = ScriptableObject.CreateInstance<T>();
            made.Add(thing);
            return thing;
        }

        [SetUp]
        public void SetUp()
        {
            profession = Make<ProfessionDefinition>();
            profession.ConfigureForTesting(AnimationCurve.Constant(0, 100, 1000));
            seed = Make<ItemDefinition>(); seed.ConfigureForTesting("bellwort_seed");
            flower = Make<ItemDefinition>(); flower.ConfigureForTesting("bellwort_flower");
            crop = Make<BotanyCropDefinition>();
            crop.ConfigureForTesting(profession, seed, flower, growthStageCount: 3, daysPerStage: 1, harvestXp: 5, id: "bellwort");
            var calendar = Make<CalendarConfig>();
            calendar.ConfigureForTesting(daysPerSeason: 28, realSecondsPerInGameMinute: 0.01f, minutesPerHour: 60, hoursPerDay: 24);
            time = new TimeService(calendar);
            professions = new ProfessionService();
            plots = new BotanyPlotService(professions, time, id => id == "bellwort" ? crop : null);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var thing in made) if (thing != null) Object.DestroyImmediate(thing);
            made.Clear();
        }

        // A day is 7 PM to 9 AM, 840 minutes; at 0.01 real seconds a minute that's 8.4 real seconds. Then the player
        // sleeps and the next day starts.
        void LiveADay()
        {
            time.Tick(8.4f + 0.05f);
            time.StartNextDay();
        }

        [Test]
        public void OnlyTilledGroundTakesACrop_AndOnlyOne()
        {
            Assert.IsFalse(plots.TryPlant(cell, crop), "untilled");
            Assert.IsTrue(plots.TryTill(cell));
            Assert.IsFalse(plots.TryTill(cell), "already tilled");
            Assert.IsTrue(plots.TryPlant(cell, crop));
            Assert.IsFalse(plots.TryPlant(cell, crop), "something grows there");
            Assert.AreEqual(0, plots.StageAt(cell));
        }

        [Test]
        public void AWateredCropGrowsAStageEachNight_AndRipensOnTheLast()
        {
            plots.TryTill(cell);
            plots.TryPlant(cell, crop);

            plots.TryWater(cell);
            LiveADay();
            Assert.AreEqual(1, plots.StageAt(cell));
            Assert.IsFalse(plots.IsMature(cell));

            plots.TryWater(cell);
            LiveADay();
            Assert.AreEqual(2, plots.StageAt(cell));
            Assert.IsTrue(plots.IsMature(cell));
        }

        [Test]
        public void GroundDriesOvernight_AndADryCropDoesntGrow()
        {
            plots.TryTill(cell);
            plots.TryPlant(cell, crop);
            Assert.IsTrue(plots.TryWater(cell));
            Assert.IsFalse(plots.TryWater(cell), "already watered today");

            LiveADay();
            Assert.IsFalse(plots.IsWatered(cell), "dry by morning");
            LiveADay();
            Assert.AreEqual(1, plots.StageAt(cell), "the dry night didn't count");
        }

        [Test]
        public void HarvestingGivesTheItemAndXp_AndLeavesTheGroundTilled()
        {
            plots.TryTill(cell);
            plots.TryPlant(cell, crop);
            Assert.IsFalse(plots.TryHarvest(cell, out _), "not ripe");
            for (int i = 0; i < 2; i++) { plots.TryWater(cell); LiveADay(); }

            Assert.IsTrue(plots.TryHarvest(cell, out var yield));
            Assert.AreSame(flower, yield);
            Assert.AreEqual(5, professions.GetState(profession).CurrentXp);
            Assert.IsTrue(plots.IsTilled(cell));
            Assert.IsFalse(plots.HasCrop(cell));
            Assert.IsTrue(plots.TryPlant(cell, crop), "ready to plant again");
        }

        [Test]
        public void ASaveKeepsTheGroundAndTheCropById()
        {
            plots.TryTill(cell);
            plots.TryPlant(cell, crop);
            plots.TryWater(cell);
            LiveADay();
            plots.TryWater(cell);
            var other = new Vector3Int(3, -1, 0);
            plots.TryTill(other);
            string json = plots.CaptureState();
            StringAssert.Contains("bellwort", json);

            var loaded = new BotanyPlotService(new ProfessionService(), time, id => id == "bellwort" ? crop : null);
            loaded.RestoreState(json);

            Assert.IsTrue(loaded.IsTilled(other));
            Assert.IsFalse(loaded.HasCrop(other));
            Assert.AreSame(crop, loaded.CropAt(cell));
            Assert.AreEqual(1, loaded.StageAt(cell));
            Assert.IsTrue(loaded.IsWatered(cell));
        }

        [Test]
        public void ProfessionXpIsSavedById()
        {
            profession.name = "Botany";
            professions.AddXp(profession, 30);
            var loaded = new ProfessionService();
            loaded.RestoreState(professions.CaptureState());
            Assert.AreEqual(30, loaded.GetState(profession).CurrentXp);
        }
    }
}
