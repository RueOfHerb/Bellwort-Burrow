using System.Linq;
using BellwortBurrow.EditorTools;
using BellwortBurrow.Tiles;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace BellwortBurrow.Tests.Editor
{
    /// <summary>The animated water (art-4): calm and fast water joining, the fast water's looks and frames, the waterfall's frames.</summary>
    public class AnimatedWaterTests
    {
        static readonly Color32 Open = new Color32(25, 36, 63, 255), Glint = new Color32(47, 83, 116, 255),
                                Bank = new Color32(59, 44, 45, 255), Streak = new Color32(36, 59, 90, 255), Clear = default;

        [Test]
        public void CalmAndFastWaterCountEachOtherAsTheSameWater()
        {
            var calm = ScriptableObject.CreateInstance<WaterTile>();
            var fast = ScriptableObject.CreateInstance<FastWaterTile>();
            var other = ScriptableObject.CreateInstance<RuleTile>();
            calm.sameWater = new TileBase[] { fast };
            fast.sameWater = new TileBase[] { calm };
            Assert.IsTrue(calm.RuleMatch(RuleTile.TilingRuleOutput.Neighbor.This, fast));
            Assert.IsFalse(calm.RuleMatch(RuleTile.TilingRuleOutput.Neighbor.NotThis, fast));
            Assert.IsTrue(fast.RuleMatch(RuleTile.TilingRuleOutput.Neighbor.This, calm));
            Assert.IsTrue(fast.RuleMatch(RuleTile.TilingRuleOutput.Neighbor.This, fast));
            Assert.IsFalse(calm.RuleMatch(RuleTile.TilingRuleOutput.Neighbor.This, other));
            Assert.IsFalse(calm.RuleMatch(RuleTile.TilingRuleOutput.Neighbor.This, null));
            Assert.IsTrue(calm.RuleMatch(RuleTile.TilingRuleOutput.Neighbor.NotThis, null));
            Object.DestroyImmediate(calm); Object.DestroyImmediate(fast); Object.DestroyImmediate(other);
        }

        [Test]
        public void FastWaterPicksEveryLookByPlaceWithCalmTheCommonest()
        {
            int[] weights = { 2, 2, 2, 2, 3 };
            var counts = new int[weights.Length];
            for (int x = -40; x < 40; x++)
                for (int y = -40; y < 40; y++)
                {
                    var p = new Vector3Int(x, y, 0);
                    int look = FastWaterTile.LookAt(p, weights);
                    Assert.AreEqual(look, FastWaterTile.LookAt(p, weights), "the same place always picks the same look");
                    counts[look]++;
                }
            Assert.IsTrue(counts.All(c => c > 0), string.Join(",", counts));
            Assert.AreEqual(counts.Max(), counts[4], "calm comes up most: " + string.Join(",", counts));
            float calmShare = counts[4] / 6400f;
            Assert.That(calmShare, Is.InRange(0.2f, 0.35f));
        }

        [Test]
        public void FastWaterHandsEachRuleItsLooksFramesAndOneCalmFrame()
        {
            var tile = ScriptableObject.CreateInstance<FastWaterTile>();
            var texture = new Texture2D(16, 16);
            tile.framesPerLook = 3;
            tile.lookWeights = new[] { 2, 2, 3 };              // two streaked looks, then calm
            int stride = tile.Stride;
            Assert.AreEqual(7, stride);
            tile.frames = Enumerable.Range(0, stride * 2).Select(i =>
            {
                var s = Sprite.Create(texture, new Rect(0, 0, 16, 16), Vector2.one * 0.5f, 16);
                s.name = i.ToString();
                return s;
            }).ToArray();
            CollectionAssert.AreEqual(new[] { "7", "8", "9" }, tile.FramesFor(1, 0).Select(s => s.name));
            CollectionAssert.AreEqual(new[] { "10", "11", "12" }, tile.FramesFor(1, 1).Select(s => s.name));
            CollectionAssert.AreEqual(new[] { "13" }, tile.FramesFor(1, 2).Select(s => s.name));
            Assert.IsNull(tile.FramesFor(2, 0), "no frames past the last rule");
            Assert.IsNull(tile.FramesFor(-1, 0));
            foreach (var s in tile.frames) Object.DestroyImmediate(s);
            Object.DestroyImmediate(texture); Object.DestroyImmediate(tile);
        }

        [Test]
        public void CalmWaterIdlesWithItsOwnPiecesFramesAndStillLooksStayStill()
        {
            var water = ScriptableObject.CreateInstance<WaterTile>();
            var texture = new Texture2D(16, 16);
            Sprite Make(string name) { var s = Sprite.Create(texture, new Rect(0, 0, 16, 16), Vector2.one * 0.5f, 16); s.name = name; return s; }
            var a = Make("a"); var b = Make("b"); var stranger = Make("c");
            water.idlePieces = new[] { a, b };
            water.idleFramesPerLook = 2;
            water.idleLookWeights = new[] { 1, 1, 4 };          // two looks, then still
            water.idleFrames = new[] { Make("a00"), a, Make("a10"), Make("a11"), Make("b00"), b, b, Make("b11") };
            CollectionAssert.AreEqual(new[] { "a00", "a" }, water.IdleFramesFor(a, 0).Select(s => s.name));
            CollectionAssert.AreEqual(new[] { "b", "b11" }, water.IdleFramesFor(b, 1).Select(s => s.name));
            Assert.IsNull(water.IdleFramesFor(a, 2), "the still look doesn't animate");
            Assert.IsNull(water.IdleFramesFor(stranger, 0), "a piece without frames stays still");
            Assert.AreNotEqual(FastWaterTile.LookAt(new Vector3Int(3, 4, 0), new[] { 1, 1, 1, 1, 1, 1, 1, 1, 4 }, WaterTile.IdleSalt) == -1, true);
            foreach (var s in water.idleFrames.Concat(new[] { stranger }).Distinct()) Object.DestroyImmediate(s);
            Object.DestroyImmediate(texture); Object.DestroyImmediate(water);
        }

        static Color32[] Piece(Color32 fill)
        {
            var p = new Color32[256];
            for (int i = 0; i < p.Length; i++) p[i] = fill;
            return p;
        }

        [Test]
        public void CalmTakesOffGlintsButKeepsBandsAndBanks()
        {
            var piece = Piece(Open);
            for (int x = 3; x < 6; x++) piece[5 * 16 + x] = Glint;                  // a glint dash, one pixel tall
            for (int x = 0; x < 16; x++) { piece[12 * 16 + x] = Glint; piece[13 * 16 + x] = Glint; }   // a band two tall
            for (int x = 0; x < 16; x++) { piece[14 * 16 + x] = Bank; piece[15 * 16 + x] = Clear; }
            piece[0 * 16 + 8] = Glint;                                               // the top row stays: nothing above to read
            var calm = FastWaterFrames.Calm(piece, Open);
            for (int x = 3; x < 6; x++) Assert.AreEqual(Open, calm[5 * 16 + x]);
            Assert.AreEqual(Glint, calm[12 * 16 + 4]);
            Assert.AreEqual(Glint, calm[13 * 16 + 4]);
            Assert.AreEqual(Bank, calm[14 * 16 + 4]);
            Assert.AreEqual(Glint, calm[8]);
        }

        [Test]
        public void StreaksGoOnlyOnOpenWater()
        {
            var calm = Piece(Open);
            for (int x = 0; x < 16; x++) calm[14 * 16 + x] = Bank;
            var streak = Piece(Clear);
            for (int y = 10; y < 16; y++) streak[y * 16 + 6] = Streak;
            var frame = FastWaterFrames.WithStreaks(calm, streak, Open);
            Assert.AreEqual(Streak, frame[10 * 16 + 6]);
            Assert.AreEqual(Streak, frame[13 * 16 + 6]);
            Assert.AreEqual(Bank, frame[14 * 16 + 6], "no streak over the bank");
            Assert.AreEqual(Open, frame[10 * 16 + 7]);
        }

        [Test]
        public void OpenWaterIsTheFullPiecesCommonestColour()
        {
            var a = Piece(Open); a[3] = Glint; a[4] = Glint;
            Assert.AreEqual(Open, FastWaterFrames.OpenWater(new[] { a, Piece(Open) }));
        }

        [Test]
        public void EverySheetWithWaterPaintsFastWaterRightAfterWaterAndTheWaterfallLast()
        {
            foreach (var ground in GroundAutoTiles.Sheets.Where(g => g.Water))
            {
                var paths = GroundAutoTiles.TilePaths(ground).ToList();
                int water = paths.IndexOf(ground.Tile("Water"));
                Assert.GreaterOrEqual(water, 0, ground.Sheet);
                Assert.AreEqual(ground.Tile("Fast Water"), paths[water + 1], ground.Sheet);
                Assert.AreEqual(ground.Tile("Waterfall"), paths.Last(), ground.Sheet);
            }
            Assert.IsTrue(GroundAutoTiles.Sheets.Single(g => g.Sheet == "faerie_field_ground").Water, "the Faerie Field has water too (art-4)");
        }

        [Test]
        public void FrameSlicesStayOutOfThePalette()
        {
            Assert.IsTrue(GroundAutoTiles.IsAnimationFrameSlice("waterfall_frame_1"));
            Assert.IsTrue(GroundAutoTiles.IsAnimationFrameSlice("fast_water_streaks"));
            Assert.IsTrue(GroundAutoTiles.IsAnimationFrameSlice("idle_water_squiggles"));
            Assert.IsFalse(GroundAutoTiles.IsAnimationFrameSlice("waterfall_set"));
            Assert.IsFalse(GroundAutoTiles.IsAnimationFrameSlice("water_set"));
        }
    }
}
