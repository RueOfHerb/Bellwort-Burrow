using BellwortBurrow.EditorTools;
using BellwortBurrow.Systems.World;
using NUnit.Framework;
using UnityEngine;

namespace BellwortBurrow.Tests.Editor
{
    public class FootprintsTests
    {
        [Test]
        public void LeftmostBaseTileLandsOnWholeNumbers()
        {
            var go = new GameObject("piece");
            try
            {
                var foot = go.AddComponent<GridFootprint>();
                foot.tilesWide = 1;
                Assert.AreEqual(new Vector3(3.5f, 2f, 0f), foot.Snapped(new Vector3(3.62f, 2.4f, 0f)));
                foot.tilesWide = 2;
                Assert.AreEqual(new Vector3(4f, 2f, 0f), foot.Snapped(new Vector3(3.62f, 1.6f, 0f)));
                foot.tilesWide = 5;
                Vector3 s = foot.Snapped(new Vector3(10.1f, 7.49f, 0f));
                Vector2 corner = foot.LeftTileCorner(s);
                Assert.AreEqual(Mathf.Round(corner.x), corner.x, 1e-5f);
                Assert.AreEqual(7f, s.y);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void SmallThingsRootOnOneTileAndTreesOnTwo()
        {
            Assert.AreEqual(1, Footprints.TilesWide("young_brown", false, null, 0, 0, null));
            Assert.AreEqual(1, Footprints.TilesWide("stump_vine", false, null, 0, 0, null));
            Assert.AreEqual(2, Footprints.TilesWide("brown_base", false, null, 0, 0, null));
            Assert.AreEqual(2, Footprints.TilesWide("giant_vine_crown", false, null, 0, 0, null));
            Assert.AreEqual(0, Footprints.TilesWide("wisp", false, null, 0, 0, null), "floating things don't snap");
            Assert.AreEqual(0, Footprints.TilesWide("thicket", true, null, 0, 0, null), "backdrops don't snap");
            Assert.AreEqual(0, Footprints.TilesWide("tree_room_blue", false, null, 0, 0, null), "tree houses go on trees");
        }

        [Test]
        public void LooseClutterGoesByThePixel_RocksStayOnTheGrid()
        {
            foreach (var n in new[] { "burrow_grass_clump_a", "burrow_grass_tall", "burrow_fern", "burrow_bellworts_b",
                                      "burrow_mushrooms", "burrow_twig", "burrow_bark_chips" })
            {
                Assert.IsTrue(Footprints.IsLooseClutter(n), n);
                Assert.AreEqual(0, Footprints.TilesWide(n, false, null, 0, 0, null), n);
            }
            Assert.IsFalse(Footprints.IsLooseClutter("burrow_mossy_rock_a"));
            Assert.AreEqual(1, Footprints.TilesWide("burrow_mossy_rock_a", false, null, 0, 0, null));
            Assert.IsFalse(Footprints.IsLooseClutter("tall_grass_a"), "the forest's tall grass stays on the grid");
            Assert.IsFalse(Footprints.IsLooseClutter("burrow_stump_foot_a"), "foot pieces are wall pieces");
        }

        [Test]
        public void TreeHousePiecesAreKnownByName()
        {
            Assert.IsTrue(Footprints.IsTreeHousePiece("tree_house_red", out var part, out var fit));
            Assert.AreEqual(TreeHouse.Part.House, part); Assert.AreEqual(TreeFit.Wood, fit);
            Assert.IsTrue(Footprints.IsTreeHousePiece("vine_tree_stairs_white", out part, out fit));
            Assert.AreEqual(TreeHouse.Part.Stairs, part); Assert.AreEqual(TreeFit.Vine, fit);
            Assert.IsFalse(Footprints.IsTreeHousePiece("round_cottage_red", out _, out _));
        }

        [Test]
        public void RoofsGoOnTheirHouse_NotOnTheGrid()
        {
            Assert.IsTrue(Footprints.IsRoof("burrow_home_wide_roof_blue", out var fits));
            Assert.AreEqual("burrow_home_wide", fits);
            Assert.AreEqual(0, Footprints.TilesWide("burrow_home_wide_roof_blue", false, null, 0, 0, null));
            var names = new[] { "burrow_home_wide_blue", "burrow_home_wide_roof_blue", "round_cottage_blue" };
            Assert.AreEqual("burrow_home_wide", Footprints.RoofSeatSize("burrow_home_wide_blue", names));
            Assert.IsNull(Footprints.RoofSeatSize("round_cottage_blue", names), "no roof of its own on the sheet");
            Assert.IsNull(Footprints.RoofSeatSize("burrow_home_wide_roof_blue", names), "a roof isn't a house");
        }

        [Test]
        public void WideThingsRootOnTheTilesUnderTheirFront()
        {
            int w = 64, h = 20;
            var art = new byte[w * h * 4];
            for (int y = 4; y < h; y++)
                for (int x = 12; x < 52; x++) art[(y * w + x) * 4 + 3] = 255;   // 40 px wide above the feet
            var slice = new SheetSlice { Name = "boulder", X = 0, Y = 0, Width = w, Height = h, HasPivot = true, PivotX = 32, PivotY = h };
            Assert.AreEqual(3, Footprints.TilesWide("boulder", false, art, w, h, slice));
        }
    }
}
