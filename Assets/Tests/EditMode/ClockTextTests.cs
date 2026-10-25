using BellwortBurrow.Core;
using BellwortBurrow.UI;
using NUnit.Framework;

namespace BellwortBurrow.Tests.EditMode
{
    public class ClockTextTests
    {
        [TestCase(19 * 60, "7:00 PM")]
        [TestCase(0, "12:00 AM")]
        [TestCase(12 * 60, "12:00 PM")]
        [TestCase(9 * 60 + 7, "9:00 AM")]
        [TestCase(23 * 60 + 59, "11:50 PM")]
        [TestCase(5 * 60 + 10, "5:10 AM")]
        public void TimeReadsAsATwelveHourClockInTens(int minutes, string expected)
        {
            Assert.AreEqual(expected, ClockText.Time(minutes));
        }

        [Test]
        public void TimeWrapsPastMidnight()
        {
            Assert.AreEqual("1:00 AM", ClockText.Time(25 * 60));
        }

        [Test]
        public void DayNamesTheDayAndSeason()
        {
            Assert.AreEqual("Day 3 · Spring", ClockText.Day(3, Season.Spring));
        }

        [TestCase("Bellwort flower", "Be")]
        [TestCase("Ax", "Ax")]
        [TestCase("  ", "?")]
        public void ShortNameIsTheFirstTwoLetters(string name, string expected)
        {
            Assert.AreEqual(expected, InventoryView.ShortName(name));
        }
    }
}
