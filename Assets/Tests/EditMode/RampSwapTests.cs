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
            " 58  63  78\tWhite fur Shadow\r\n140 147 166\tWhite fur Dark\r\n201 205 214\tWhite fur Base\r\n237 237 239\tWhite fur Light\r\n255 255 255\tWhite fur Highlight\r\n" +
            "123  75  27\tFox fur Shadow\r\n157 103  28\tFox fur Dark\r\n191 139  31\tFox fur Base\r\n228 198  79\tFox fur Light\r\n253 247 201\tFox fur Highlight\r\n" +
            " 49  24  15\tRabbit fur Shadow\r\n 71  41  25\tRabbit fur Dark\r\n 92  61  36\tRabbit fur Base\r\n118  90  62\tRabbit fur Light\r\n142 122  95\tRabbit fur Highlight\r\n" +
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
            fox.ConfigureForTesting("fox", "Fox", null, "Fox fur", "Rabbit fur", "Eyes");
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
            Assert.AreEqual("White fur", ramps[0].name);
            Assert.AreEqual("Gold eyes", ramps[4].name);
            Assert.AreEqual(5, ramps[1].colors.Length);
            Assert.AreEqual(new Color32(123, 75, 27, 255), ramps[1].colors[0]);
            Assert.AreEqual(new Color32(253, 247, 201, 255), ramps[1].colors[4]);
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

            Assert.AreEqual("Fox fur", palette.Find("Fox fur").name);
            Assert.AreEqual("Fox fur", palette.Find("fox-fur").name);
            Assert.IsNull(palette.Find(RampSwap.Natural));
            Assert.IsNull(palette.Find(""));
            Assert.IsNull(palette.Find(null));
        }

        [Test]
        public void Natural_SwapsNothing()
        {
            int count = Build(new PlayerProfile { furColor = RampSwap.Natural, markingsColor = "", eyeColor = "otter-fur" }, out var from, out _);

            Assert.AreEqual(0, count);
            Assert.AreEqual(0, from.Count);
        }

        [Test]
        public void FurColor_SwapsTheKindsFurRampShadeForShade()
        {
            int count = Build(new PlayerProfile { furColor = "white-fur" }, out var from, out var to);

            Assert.AreEqual(5, count);
            Assert.AreEqual(new Color32(123, 75, 27, 255), from[0]);
            Assert.AreEqual(new Color32(58, 63, 78, 255), to[0]);
            Assert.AreEqual(new Color32(253, 247, 201, 255), from[4]);
            Assert.AreEqual(new Color32(255, 255, 255, 255), to[4]);
        }

        [Test]
        public void PickingTheKindsOwnRamp_SwapsNothing()
        {
            Assert.AreEqual(0, Build(new PlayerProfile { furColor = "fox-fur", markingsColor = "Rabbit fur" }, out _, out _));
        }

        [Test]
        public void FurMarkingsAndEyes_AllSwap_AndMaySharePicks()
        {
            int count = Build(new PlayerProfile { furColor = "white-fur", markingsColor = "white-fur", eyeColor = "gold-eyes" }, out var from, out var to);

            Assert.AreEqual(15, count);
            Assert.AreEqual(new Color32(49, 24, 15, 255), from[5], "markings start with Rabbit fur Shadow");
            Assert.AreEqual(new Color32(58, 63, 78, 255), to[5], "and turn White fur Shadow, the same as the fur");
            Assert.AreEqual(new Color32(104, 72, 26, 255), to[10]);
        }
    }
}
