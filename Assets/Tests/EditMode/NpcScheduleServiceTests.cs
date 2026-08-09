using NUnit.Framework;
using UnityEngine;
using BellwortBurrow.Data;
using BellwortBurrow.Systems.Npcs;

namespace BellwortBurrow.Tests.EditMode
{
    public class NpcScheduleServiceTests
    {
        [Test]
        public void GetCurrentLocationId_ReturnsScheduledLocationOrHomeFallback()
        {
            var schedule = ScriptableObject.CreateInstance<ScheduleDefinition>();
            schedule.ConfigureForTesting(new[]
            {
                new ScheduleEntry { timeOfDayMinutes = 480, locationId = "Farm" },
                new ScheduleEntry { timeOfDayMinutes = 1080, locationId = "Tavern" },
            });

            var npc = ScriptableObject.CreateInstance<NpcDefinition>();
            npc.ConfigureForTesting("Home", schedule);

            var service = new NpcScheduleService();
            service.Register(npc);

            Assert.AreEqual("Home", service.GetCurrentLocationId(npc, 100));
            Assert.AreEqual("Farm", service.GetCurrentLocationId(npc, 500));
            Assert.AreEqual("Tavern", service.GetCurrentLocationId(npc, 1200));
        }
    }
}
