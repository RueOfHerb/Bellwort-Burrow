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
        [Test]
        public void TryHarvest_AfterCropMatures_YieldsItemAndGrantsProfessionXp()
        {
            var profession = ScriptableObject.CreateInstance<ProfessionDefinition>();
            profession.ConfigureForTesting(AnimationCurve.Constant(0, 100, 1000));

            var seed = ScriptableObject.CreateInstance<ItemDefinition>();
            var harvestItem = ScriptableObject.CreateInstance<ItemDefinition>();

            var crop = ScriptableObject.CreateInstance<BotanyCropDefinition>();
            crop.ConfigureForTesting(profession, seed, harvestItem, growthStageCount: 2, daysPerStage: 1, harvestXp: 5);

            var calendar = ScriptableObject.CreateInstance<CalendarConfig>();
            calendar.ConfigureForTesting(daysPerSeason: 28, realSecondsPerInGameMinute: 0.01f, minutesPerHour: 60, hoursPerDay: 24);
            var timeService = new TimeService(calendar);
            var professionService = new ProfessionService();
            var plotService = new BotanyPlotService(professionService, timeService);

            var cell = new Vector3Int(0, 0, 0);
            Assert.IsTrue(plotService.TryPlant(cell, crop));
            Assert.IsFalse(plotService.TryPlant(cell, crop));
            Assert.IsFalse(plotService.IsMature(cell));

            // One in-game day is 1440 minutes; at 0.01 real seconds/minute that's 14.4 real seconds.
            timeService.Tick(14.4f + 0.05f);
            Assert.IsFalse(plotService.IsMature(cell));

            timeService.Tick(14.4f + 0.05f);
            Assert.IsTrue(plotService.IsMature(cell));

            bool harvested = plotService.TryHarvest(cell, out var yieldItem);

            Assert.IsTrue(harvested);
            Assert.AreEqual(harvestItem, yieldItem);
            Assert.AreEqual(5, professionService.GetState(profession).CurrentXp);
            Assert.IsFalse(plotService.IsMature(cell));
        }
    }
}
