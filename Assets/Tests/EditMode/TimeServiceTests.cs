using NUnit.Framework;
using UnityEngine;
using BellwortBurrow.Core;
using BellwortBurrow.Data;
using BellwortBurrow.Systems.Calendar;

namespace BellwortBurrow.Tests.EditMode
{
    public class TimeServiceTests
    {
        const int SevenPm = 19 * 60, Midnight = 0, NineAm = 9 * 60;

        CalendarConfig calendar;

        [SetUp]
        public void SetUp()
        {
            calendar = ScriptableObject.CreateInstance<CalendarConfig>();
            // 0.01 real seconds a minute: a day, 7 PM to 9 AM, is 840 minutes, 8.4 real seconds.
            calendar.ConfigureForTesting(daysPerSeason: 28, realSecondsPerInGameMinute: 0.01f, minutesPerHour: 60, hoursPerDay: 24);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(calendar);

        static void TickMinutes(TimeService time, int minutes) => time.Tick(minutes * 0.01f + 0.005f);

        [Test]
        public void ANewGame_StartsOnDayOneAtSevenPm()
        {
            var time = new TimeService(calendar);

            Assert.AreEqual(1, time.CurrentDay);
            Assert.AreEqual(SevenPm, time.MinutesSinceMidnight);
            Assert.AreEqual(0, time.MinutesAwake);
            Assert.AreEqual(840, calendar.AwakeMinutes);
        }

        [Test]
        public void Midnight_KeepsTheSameDay()
        {
            var time = new TimeService(calendar);
            int dayChanges = 0;
            time.OnDayChanged += () => dayChanges++;

            TickMinutes(time, 5 * 60 + 30);

            Assert.AreEqual(Midnight + 30, time.MinutesSinceMidnight);
            Assert.AreEqual(1, time.CurrentDay);
            Assert.AreEqual(0, dayChanges);
        }

        [Test]
        public void NineAm_EndsTheDayAndHoldsTheClock()
        {
            var time = new TimeService(calendar);
            int ended = 0, dayChanges = 0;
            time.OnDayEnded += () => ended++;
            time.OnDayChanged += () => dayChanges++;

            TickMinutes(time, 839);
            Assert.IsFalse(time.DayOver);
            TickMinutes(time, 1);

            Assert.IsTrue(time.DayOver);
            Assert.AreEqual(NineAm, time.MinutesSinceMidnight);
            Assert.AreEqual(1, ended);
            Assert.AreEqual(0, dayChanges, "The next day starts when the player wakes, not when they pass out.");

            TickMinutes(time, 60);
            Assert.AreEqual(NineAm, time.MinutesSinceMidnight, "The clock ran on after the day was over.");
            Assert.AreEqual(1, ended);
        }

        [Test]
        public void StartNextDay_WakesThePlayerAtSevenPmOnTheNextDay()
        {
            var time = new TimeService(calendar);
            int dayChanges = 0;
            time.OnDayChanged += () => dayChanges++;
            TickMinutes(time, 840);

            time.StartNextDay();

            Assert.IsFalse(time.DayOver);
            Assert.AreEqual(2, time.CurrentDay);
            Assert.AreEqual(SevenPm, time.MinutesSinceMidnight);
            Assert.AreEqual(1, dayChanges);
        }

        [Test]
        public void ADayEndingInsideTheTick_CanStartTheNextDayAtOnce()
        {
            var time = new TimeService(calendar);
            time.OnDayEnded += time.StartNextDay;

            TickMinutes(time, 900);

            Assert.AreEqual(2, time.CurrentDay);
            Assert.AreEqual(SevenPm, time.MinutesSinceMidnight, "Time left over from the old day spilled into the new one.");
        }

        [Test]
        public void TheLastDayOfASeason_TurnsTheSeason()
        {
            calendar.ConfigureForTesting(daysPerSeason: 2, realSecondsPerInGameMinute: 0.01f, minutesPerHour: 60, hoursPerDay: 24);
            var time = new TimeService(calendar);
            Season turnedTo = Season.Spring;
            time.OnSeasonChanged += season => turnedTo = season;

            time.StartNextDay();
            Assert.AreEqual(Season.Spring, time.CurrentSeason);
            time.StartNextDay();

            Assert.AreEqual(3, time.CurrentDay);
            Assert.AreEqual(Season.Summer, time.CurrentSeason);
            Assert.AreEqual(Season.Summer, turnedTo);
        }

        [Test]
        public void ASaveFromOutsideTheWakingHours_EndsItsDay()
        {
            var time = new TimeService(calendar);
            time.RestoreState("{\"day\":4,\"season\":\"Spring\",\"minutesSinceMidnight\":600}");
            int ended = 0;
            time.OnDayEnded += () => ended++;

            time.Tick(0.01f);

            Assert.IsTrue(time.DayOver);
            Assert.AreEqual(1, ended);
            Assert.AreEqual(4, time.CurrentDay);
        }

        [Test]
        public void SaveAndRestore_KeepTheDayAndTime()
        {
            var time = new TimeService(calendar);
            TickMinutes(time, 200);
            time.StartNextDay();
            TickMinutes(time, 300);

            var later = new TimeService(calendar);
            later.RestoreState(time.CaptureState());

            Assert.AreEqual(2, later.CurrentDay);
            Assert.AreEqual((SevenPm + 300) % 1440, later.MinutesSinceMidnight);
            Assert.AreEqual(300, later.MinutesAwake);
        }
    }
}
