using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using BellwortBurrow.EditorTools;

namespace BellwortBurrow.Tests.Editor
{
    /// <summary>The art spec checks and the Sync Ledger fingerprint, on small made-up sheets.</summary>
    public class ArtRulesTests
    {
        const int W = 20, H = 20;

        static SheetSlice Slice(string name, int x, int y, int w, int h, int px, int py) =>
            new SheetSlice { Name = name, X = x, Y = y, Width = w, Height = h, HasPivot = true, PivotX = px, PivotY = py };

        static void Paint(byte[] rgba, int x, int y, byte[] rgb, byte alpha)
        {
            int i = (y * W + x) * 4;
            rgba[i] = rgb[0]; rgba[i + 1] = rgb[1]; rgba[i + 2] = rgb[2]; rgba[i + 3] = alpha;
        }

        [Test]
        public void FingerprintIsTheFirstEightCharactersOfTheSha1()
        {
            Assert.AreEqual("a9993e36", ArtRules.Fingerprint(Encoding.ASCII.GetBytes("abc")));
        }

        [Test]
        public void PivotMustSitAtTheBottomMiddle()
        {
            Assert.IsTrue(ArtRules.PivotAtFeet(Slice("tree", 0, 0, 20, 14, 10, 14)));
            Assert.IsTrue(ArtRules.PivotAtFeet(Slice("home", 0, 0, 20, 17, 10, 14)), "a base may reach a few pixels below its feet");
            Assert.IsFalse(ArtRules.PivotAtFeet(Slice("tree", 0, 0, 20, 14, 10, 7)));
            Assert.IsFalse(ArtRules.PivotAtFeet(Slice("tree", 0, 0, 20, 14, 2, 14)));
        }

        [Test]
        public void PiecesThatGoTogetherStandStackedOnOneCenterLine()
        {
            var tree = new List<SheetSlice>
            {
                Slice("giant_oak_crown", 0, 0, 40, 30, 20, 30),
                Slice("giant_oak_middle", 14, 30, 12, 10, 6, 10),
                Slice("giant_oak_base", 10, 40, 20, 10, 10, 10),
            };
            Assert.IsEmpty(ArtRules.StandTogether(tree));
            tree[1] = Slice("giant_oak_middle", 15, 30, 12, 10, 6, 10);
            Assert.AreEqual(2, ArtRules.StandTogether(tree).Count, "off the center line, so off its base and under its crown");
            tree[1] = Slice("giant_oak_middle", 14, 28, 12, 10, 6, 10);
            Assert.AreEqual(2, ArtRules.StandTogether(tree).Count, "a gap below the crown and the middle not on the base");

            var house = new List<SheetSlice>
            {
                Slice("tree_room_red", 0, 0, 20, 6, 10, 6),
                Slice("tree_stairs_red", 0, 6, 20, 8, 10, 1),
                Slice("tree_house_red", 0, 14, 20, 12, 10, 11),
            };
            Assert.IsEmpty(ArtRules.StandTogether(house));
            house[0] = Slice("tree_room_red", 30, 0, 20, 6, 10, 6);
            Assert.AreEqual(1, ArtRules.StandTogether(house).Count, "the room isn't on its stairs");
        }

        [Test]
        public void ARoofStandsOnItsHouse_WithItsPivotAtTheHousesFeet()
        {
            // The roof's slice stands on its house's; its pivot is below it, where the house's feet are once it's on.
            var homes = new List<SheetSlice>
            {
                Slice("burrow_home_roof_red", 0, 0, 20, 8, 10, 15),
                Slice("burrow_home_red", 0, 8, 20, 12, 10, 12),
            };
            Assert.IsEmpty(ArtRules.StandTogether(homes));
            Assert.IsEmpty(ArtRules.Check("burrow_buildings", W, H, new byte[W * H * 4], null, homes, false),
                "a roof's pivot is its house's feet, not its own");
            Assert.AreEqual("burrow_home_red", ArtRules.HouseOf("burrow_home_roof_red"));
            homes[0] = Slice("burrow_home_roof_red", 0, 0, 20, 7, 10, 15);
            Assert.AreEqual(1, ArtRules.StandTogether(homes).Count, "a gap between the roof and its house");
            homes[0] = Slice("burrow_home_roof_red", 0, 0, 20, 8, 10, 8);
            Assert.AreEqual(1, ArtRules.Check("burrow_buildings", W, H, new byte[W * H * 4], null, homes, false).Count,
                "a roof with its pivot on itself would stand on the ground, not on its house");
        }

        [Test]
        public void TouchingSlicesDontOverlapButCrossingOnesDo()
        {
            var touching = new List<SheetSlice> { Slice("a", 0, 0, 10, 10, 5, 10), Slice("b", 10, 0, 10, 10, 5, 10) };
            Assert.IsEmpty(ArtRules.Overlaps(touching));
            var crossing = new List<SheetSlice> { Slice("a", 0, 0, 10, 10, 5, 10), Slice("b", 9, 9, 10, 10, 5, 10) };
            Assert.AreEqual(1, ArtRules.Overlaps(crossing).Count);
        }

        [Test]
        public void AHomeNeedsItsSigilAndItsShadow()
        {
            var slices = new List<SheetSlice> { Slice("round_cottage_red", 0, 0, W, H, W / 2, H) };
            var art = new byte[W * H * 4];
            var glow = new byte[W * H * 4];
            Assert.AreEqual(2, ArtRules.Check("forest_buildings", W, H, art, glow, slices, false).Count);

            for (int i = 0; i < 25; i++) Paint(glow, i % W, i / W, ArtRules.SigilRed, 255);
            for (int i = 0; i < 45; i++) Paint(art, i % W, 10 + i / W, ArtRules.ShadowNavy, 112);
            Assert.IsEmpty(ArtRules.Check("forest_buildings", W, H, art, glow, slices, false));
        }

        [Test]
        public void OnlyPartTransparentNavyCountsAsShadow()
        {
            var slice = Slice("hex_cottage_red", 0, 0, W, H, W / 2, H);
            var art = new byte[W * H * 4];
            for (int i = 0; i < 50; i++) Paint(art, i % W, i / W, ArtRules.ShadowNavy, 255);
            Assert.AreEqual(0, ArtRules.CountColor(art, W, H, slice, ArtRules.ShadowNavy, true));
            Assert.AreEqual(50, ArtRules.CountColor(art, W, H, slice, ArtRules.ShadowNavy, false));
        }

        [Test]
        public void FloatingThingsAndTileSheetsSkipThePivotRule()
        {
            var wisp = new List<SheetSlice> { Slice("wisp", 0, 0, 8, 8, 4, 4) };
            Assert.IsEmpty(ArtRules.Check("forest_props", W, H, new byte[W * H * 4], null, wisp, false));
            var tile = new List<SheetSlice> { Slice("grass", 0, 0, 16, 16, 8, 8) };
            Assert.IsEmpty(ArtRules.Check("forest_ground", W, H, new byte[W * H * 4], null, tile, true));
        }
    }
}
