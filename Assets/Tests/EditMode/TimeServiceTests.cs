using NUnit.Framework;
using UnityEngine;
using BellwortBurrow.Data;
using BellwortBurrow.Systems.Calendar;

namespace BellwortBurrow.Tests.EditMode
{
    public class TimeServiceTests
    {
        [Test]
        public void Tick_PastDayBoundary_RaisesOnDayChangedAndAdvancesDay()
        {
            var calendar = ScriptableObject.CreateInstance<CalendarConfig>();
            calendar.ConfigureForTesting(daysPerSeason: 28, realSecondsPerInGameMinute: 0.01f, minutesPerHour: 60, hoursPerDay: 24);

            var timeService = new TimeService(calendar);
            int dayChangedCount = 0;
            timeService.OnDayChanged += () => dayChangedCount++;
            int startingDay = timeService.CurrentDay;

            // One full in-game day is 1440 minutes; at 0.01 real seconds/minute that's 14.4 real seconds.
            timeService.Tick(14.4f + 0.1f);

            Assert.AreEqual(1, dayChangedCount);
            Assert.AreEqual(startingDay + 1, timeService.CurrentDay);
        }
    }
}
