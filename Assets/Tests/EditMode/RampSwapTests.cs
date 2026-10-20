using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using BellwortBurrow.Data;

namespace BellwortBurrow.Tests.EditMode
{
    public class RampSwapTests
    {
        const string Gpl =
            "GIMP Palette\r\nName: Characters\r\nColumns: 5\r\n#\r\n" +
            " 58  63  78\tWhite Shadow\r\n140 147 166\tWhite Dark\r\n201 205 214\tWhite Base\r\n237 237 239\tWhite Light\r\n255 255 255\tWhite Highlight\r\n" +
            " 74  40  22\tOrange Shadow\r\n123  75  27\tOrange Dark\r\n157 103  28\tOrange Base\r\n191 139  31\tOrange Light\r\n228 198  79\tOrange Highlight\r\n" +
            " 30  16  10\tDark brown Shadow\r\n 48  26  16\tDark brown Dark\r\n 70  40  24\tDark brown Base\r\n 98  64  42\tDark brown Light\r\n128  96  70\tDark brown Highlight\r\n" +
            " 27  29  38\tEyes Shadow\r\n 45  49  64\tEyes Dark\r\n 66  72  92\tEyes Base\r\n 94 102 124\tEyes Light\r\n128 136 158\tEyes Highlight\r\n" +
            "104  72  26\tGold eyes Shadow\r\n172 130  38\tGold eyes Dark\r\n220 178  48\tGold eyes Base\r\n242 210  96\tGold eyes Light\r\n252 240 176\tGold eyes Highlight\r\n";

        static RampPalette MakePalette()
        {
            var palette = ScriptableObject.CreateInstance<RampPalette>();
            palette.SetRamps(RampPalette.Parse(Gpl));
            return palette;
        }

        static CritterKind Fox()
        {
            var fox = ScriptableObject.CreateInstance<CritterKind>();
            fox.ConfigureForTesting("fox", "Fox", null, "Orange", "Dark brown", "Eyes");
            return fox;
        }

        static int Build(PlayerProfile profile, out List<Color32> from, out List<Color32> to)
        {
            from = new List<Color32>();
            to = new List<Color32>();
            return RampSwap.Build(Fox(), profile, MakePalette(), from, to);
        }

        [Test]
        public void Parse_MakesOneNamedRampPerRow()
        {
            var ramps = RampPalette.Parse(Gpl);

            Assert.AreEqual(5, ramps.Count);
            Assert.AreEqual("White", ramps[0].name);
            Assert.AreEqual("Gold eyes", ramps[4].name);
            Assert.AreEqual(5, ramps[1].colors.Length);
            Assert.AreEqual(new Color32(74, 40, 22, 255), ramps[1].colors[0]);
            Assert.AreEqual(new Color32(228, 198, 79, 255), ramps[1].colors[4]);
            Assert.AreEqual("Dark brown", ramps[2].name);
        }

        [Test]
        public void Parse_ARowWhoseNamesAllMatch_KeepsTheWholeName()
        {
            string gpl = "GIMP Palette\nColumns: 2\n#\n85 15 32\tGarnet to ruby\n137 31 47\tGarnet to ruby\n1 2 3\tStone Shadow \n4 5 6\tStone Dark \n";

            var ramps = RampPalette.Parse(gpl);

            Assert.AreEqual("Garnet to ruby", ramps[0].name);
            Assert.AreEqual("Stone", ramps[1].name);
        }

        [Test]
        public void Find_TakesANameOrAnId_AndNaturalFindsNothing()
        {
            var palette = MakePalette();

            Assert.AreEqual("Dark brown", palette.Find("Dark brown").name);
            Assert.AreEqual("Dark brown", palette.Find("dark-brown").name);
            Assert.IsNull(palette.Find(RampSwap.Natural));
            Assert.IsNull(palette.Find(""));
            Assert.IsNull(palette.Find(null));
        }

        [Test]
        public void Natural_SwapsNothing()
        {
            int count = Build(new PlayerProfile { furColor = RampSwap.Natural, markingsColor = "", eyeColor = "sea-green" }, out var from, out _);

            Assert.AreEqual(0, count);
            Assert.AreEqual(0, from.Count);
        }

        [Test]
        public void FurColor_SwapsTheKindsFurRampShadeForShade()
        {
            int count = Build(new PlayerProfile { furColor = "white" }, out var from, out var to);

            Assert.AreEqual(5, count);
            Assert.AreEqual(new Color32(74, 40, 22, 255), from[0]);
            Assert.AreEqual(new Color32(58, 63, 78, 255), to[0]);
            Assert.AreEqual(new Color32(228, 198, 79, 255), from[4]);
            Assert.AreEqual(new Color32(255, 255, 255, 255), to[4]);
        }

        [Test]
        public void PickingTheKindsOwnRamp_SwapsNothing()
        {
            Assert.AreEqual(0, Build(new PlayerProfile { furColor = "orange", markingsColor = "Dark brown" }, out _, out _));
        }

        [Test]
        public void FurMarkingsAndEyes_AllSwap_AndMaySharePicks()
        {
            int count = Build(new PlayerProfile { furColor = "white", markingsColor = "white", eyeColor = "gold-eyes" }, out var from, out var to);

            Assert.AreEqual(15, count);
            Assert.AreEqual(new Color32(30, 16, 10, 255), from[5], "markings start with Dark brown Shadow");
            Assert.AreEqual(new Color32(58, 63, 78, 255), to[5], "and turn White Shadow, the same as the fur");
            Assert.AreEqual(new Color32(104, 72, 26, 255), to[10]);
        }
    }
}
