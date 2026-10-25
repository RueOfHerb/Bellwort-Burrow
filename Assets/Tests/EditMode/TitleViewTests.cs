using System.Collections.Generic;
using BellwortBurrow.Systems.Save;
using BellwortBurrow.UI;
using NUnit.Framework;

namespace BellwortBurrow.Tests.EditMode
{
    public class TitleViewTests
    {
        static SaveHeader Header(string slot, string savedAt, string name = "Rue", int day = 1, string zone = "BellwortBurrow") =>
            new SaveHeader { slot = slot, savedAt = savedAt, playerName = name, day = day, zone = zone };

        [Test]
        public void NewestIsTheLatestSavedAt()
        {
            var headers = new List<SaveHeader>
            {
                Header("slot1", "2026-10-24T10:00:00Z"),
                Header("dev", "2026-10-25T09:00:00Z"),
                Header("slot2", "2026-10-25T08:00:00Z")
            };

            Assert.AreEqual("dev", TitleView.Newest(headers).slot);
        }

        [Test]
        public void NewestIsNullWithNoSaves()
        {
            Assert.IsNull(TitleView.Newest(null));
            Assert.IsNull(TitleView.Newest(new List<SaveHeader>()));
        }

        [Test]
        public void SaveTextNamesWhoWhenAndWhere()
        {
            Assert.AreEqual("Rue · Day 3 · Bellwort Burrow", TitleView.SaveText(Header("slot1", "", day: 3)));
            Assert.AreEqual("Someone · Day 1", TitleView.SaveText(Header("slot1", "", name: " ", zone: "")));
        }

        [TestCase("BellwortBurrow", "Bellwort Burrow")]
        [TestCase("ForlornForest", "Forlorn Forest")]
        [TestCase("ThicketPath", "Thicket Path")]
        [TestCase("", "")]
        public void ZoneNameSpacesTheSceneName(string scene, string expected)
        {
            Assert.AreEqual(expected, TitleView.ZoneName(scene));
        }
    }
}
