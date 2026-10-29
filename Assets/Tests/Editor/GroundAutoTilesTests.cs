using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;
using BellwortBurrow.EditorTools;

namespace BellwortBurrow.Tests.Editor
{
    public class GroundAutoTilesTests
    {
        // Neighbour bits, y up: top row 64 128 256, middle 8 16 32, bottom 1 2 4.
        static readonly uint[] Around = { 1, 2, 4, 8, 32, 64, 128, 256 };
        static readonly uint[] RuleOrder = { 64, 128, 256, 8, 32, 1, 2, 4 }; // NW, N, NE, W, E, SW, S, SE

        static IEnumerable<uint> EveryNeighbourhood()
        {
            for (int i = 0; i < 256; i++)
            {
                uint around = 16;
                for (int b = 0; b < 8; b++) if ((i & (1 << b)) != 0) around |= Around[b];
                yield return around;
            }
        }

        static HashSet<uint> TemplateLayouts()
        {
            var t = GroundAutoTiles.Template;
            var have = new HashSet<uint>();
            for (int row = 0; row < t.Length; row++)
                for (int column = 0; column < t[row].Length; column++)
                    if (t[row][column] == '#') have.Add(GroundAutoTiles.Layout(t, column, row));
            return have;
        }

        [TestCase(new[] { "...", ".#.", "..." }, 16u)]                        // on its own
        [TestCase(new[] { "###", "###", "###" }, 511u)]                       // surrounded
        [TestCase(new[] { "...", "###", "..." }, 8u + 16 + 32)]               // the middle of a one tile path
        [TestCase(new[] { "#.#", ".#.", "#.#" }, 16u)]                        // corners alone don't count
        [TestCase(new[] { "##.", "##.", "..." }, 64u + 128 + 8 + 16)]         // bottom right of a block
        [TestCase(new[] { ".#.", "###", ".#." }, 2u + 8 + 16 + 32 + 128)]    // a crossroads
        [TestCase(new[] { "#..", "##.", ".##" }, 64u + 8 + 16 + 2 + 4)]      // a stair step: the corners past each end carry on
        [TestCase(new[] { "...", "##.", ".##" }, 8u + 16 + 2 + 4)]           // the top of a staircase: it carries on only down
        public void Layout_ReadsNeighbours(string[] grid, uint expected)
        {
            Assert.AreEqual(expected, GroundAutoTiles.Layout(grid, 1, 1));
        }

        [Test]
        public void Template_HoldsEveryLayout()
        {
            var t = GroundAutoTiles.Template;
            for (int row = 0; row < t.Length; row++)
                Assert.AreEqual(t[0].Length, t[row].Length, $"row {row} is a different length");

            var every = new HashSet<uint>();
            foreach (var around in EveryNeighbourhood()) every.Add(GroundAutoTiles.LayoutOf(around));
            Assert.AreEqual(GroundAutoTiles.LayoutCount, every.Count);
            var have = TemplateLayouts();
            foreach (var layout in every) Assert.IsTrue(have.Contains(layout), $"the template lacks layout {layout}");
            Assert.IsTrue(have.Contains(GroundAutoTiles.Full));
        }

        [Test]
        public void RuleNeighbours_EveryNeighbourhoodMatchesOnlyItsOwnRule()
        {
            var rules = new List<(uint layout, int[] conditions)>();
            foreach (var layout in TemplateLayouts()) rules.Add((layout, GroundAutoTiles.RuleNeighbours(layout)));

            foreach (var around in EveryNeighbourhood())
            {
                var matched = new List<uint>();
                foreach (var (layout, conditions) in rules)
                {
                    bool ok = true;
                    for (int i = 0; i < 8 && ok; i++)
                    {
                        bool painted = (around & RuleOrder[i]) != 0;
                        if (conditions[i] == RuleTile.TilingRuleOutput.Neighbor.This) ok = painted;
                        else if (conditions[i] == RuleTile.TilingRuleOutput.Neighbor.NotThis) ok = !painted;
                    }
                    if (ok) matched.Add(layout);
                }
                Assert.AreEqual(1, matched.Count, $"neighbourhood {around} matches {matched.Count} rules");
                Assert.AreEqual(GroundAutoTiles.LayoutOf(around), matched[0]);
            }
        }

        [Test]
        public void FacePieceKeys_FitTheFaceSetAndCoverEveryLook()
        {
            var keys = GroundAutoTiles.FacePieceKeys();
            Assert.LessOrEqual(keys.Count, GroundAutoTiles.FaceSetColumns * GroundAutoTiles.FaceSetRows);
            foreach (char row in "TMBS")
                foreach (char west in "cobk")
                    foreach (char east in "cobk")
                        Assert.Contains($"F{row}{west}{east}", keys);
            foreach (char shape in "DU")
                for (int place = 0; place < 4; place++)
                    foreach (var ends in new[] { "cc", "oc", "co" })
                        Assert.Contains($"{shape}{place}{ends}", keys);
            Assert.AreEqual(12, keys.Count(k => k == "FMcc"), "a plain face has twelve variations");
        }

        [Test]
        public void PieceName_NumbersTilesLikeTheImporter()
        {
            Assert.AreEqual("path_set_000", GroundAutoTiles.PieceName("path_set", 0, 0, GroundAutoTiles.Template));
            Assert.AreEqual("path_set_018", GroundAutoTiles.PieceName("path_set", 1, 1, GroundAutoTiles.Template));
        }

        [TestCase(false, true, false, true, 'T', 'l')]
        [TestCase(false, true, true, true, 'T', 'm')]
        [TestCase(true, true, true, false, 'M', 'r')]
        [TestCase(true, false, true, true, 'B', 'm')]
        [TestCase(true, false, false, false, 'B', 's')]
        [TestCase(false, false, true, true, 'T', 'm')]
        public void WaterfallPiece_TopMiddleBottomAndItsEnds(bool n, bool s, bool w, bool e, char row, char side)
        {
            // A fall is three rows tall like the face it runs over, and as wide as it's painted.
            Assert.AreEqual((row, side), GroundAutoTiles.WaterfallPiece(n, s, w, e));
        }

        [Test]
        public void EveryGroundSheet_MakesGrassAPathAndADeerTrail()
        {
            var every = new HashSet<string>();
            foreach (var ground in GroundAutoTiles.Sheets)
            {
                var names = GroundAutoTiles.TilePaths(ground).Select(System.IO.Path.GetFileNameWithoutExtension).ToList();
                var kinds = new[] { "Grass", "Path", "Deer Trail" }.ToList();
                if (ground.Stone) kinds.Add("Flagstones");
                if (ground.Water) kinds.Add("Water");
                if (ground.Farm) kinds.AddRange(new[] { "Tillable Dirt", "Farmland", "Watered Farmland" });
                if (ground.Banks) kinds.AddRange(new[] { "Bank Face", "Cliff Face", "Mossy Cliff Face", "Root Bank Face",
                    "Stone Steps", "Log Steps", "Earth Ramp North", "Earth Ramp East", "Earth Ramp West",
                    "Stone Ramp North", "Stone Ramp East", "Stone Ramp West" });
                if (ground.Banks && ground.Water) kinds.Add("Waterfall");
                CollectionAssert.AreEqual(kinds.Select(k => ground.Zone + " " + k), names, ground.Sheet);
                foreach (var path in GroundAutoTiles.TilePaths(ground))
                    Assert.IsTrue(every.Add(path), $"{path} comes from two ground sheets");
            }
            Assert.AreEqual(GroundAutoTiles.Sheets.Length, GroundAutoTiles.Sheets.Select(g => g.Palette).Distinct().Count(), "two ground sheets share a palette");
        }
        [Test]
        public void EvenPick_TakesEveryPieceAboutEquallyOften_AndNeighboursSeldomMatch()
        {
            var pieces = Enumerable.Range(0, 12).Select(i => { var sp = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.zero); sp.name = "p" + i; return sp; }).ToList();
            float scale = GroundAutoTiles.ScatterPerlinScale;
            var laid = GroundAutoTiles.EvenPick(pieces, scale);
            Sprite Pick(int x, int y) => laid[Mathf.Clamp(Mathf.FloorToInt(Mathf.PerlinNoise((x + 100000f) * scale, (y + 100000f) * scale) * laid.Length), 0, laid.Length - 1)];
            var counts = pieces.ToDictionary(p => p, p => 0);
            int same = 0, cells = 0;
            for (int y = 0; y < 100; y++)
                for (int x = 0; x < 140; x++)
                {
                    var p = Pick(x, y);
                    counts[p]++;
                    if (Pick(x + 1, y) == p) same++;
                    cells++;
                }
            float even = cells / 12f;
            foreach (var kv in counts)
                Assert.That(kv.Value, Is.InRange(even * 0.6f, even * 1.4f), $"{kv.Key.name} is picked {kv.Value} times of {cells}");
            Assert.That(same / (float)cells, Is.LessThan(0.2f), "neighbours land on the same piece too often");
            foreach (var p in pieces) UnityEngine.Object.DestroyImmediate(p);
        }

        [Test]
        public void OnlyTheBurrowPicksEvenly_TheForestKeepsItsPlainGrassCommonest()
        {
            Assert.IsTrue(GroundAutoTiles.Sheets.Single(g => g.Sheet == "burrow_ground").EvenPick);
            Assert.IsFalse(GroundAutoTiles.Sheets.Single(g => g.Sheet == "forest_ground").EvenPick);
        }
        static Sprite Named(string name)
        {
            var sp = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.zero);
            sp.name = name;
            return sp;
        }

        [Test]
        public void WeightedPick_TakesThePlainPiecesTwiceAsOftenAsTheFeaturedOnes()
        {
            var plain = Enumerable.Range(0, 4).Select(i => Named($"grass_{i:00}")).ToList();
            var featured = Enumerable.Range(0, 8).Select(i => Named($"grass_more_{i:00}")).ToList();
            var laid = GroundAutoTiles.EvenPick(plain.Concat(featured).ToList(), GroundAutoTiles.ScatterPerlinScale);
            float scale = GroundAutoTiles.ScatterPerlinScale;
            int plainCount = 0, cells = 0;
            for (int y = 0; y < 100; y++)
                for (int x = 0; x < 140; x++, cells++)
                    if (plain.Contains(laid[Mathf.Clamp(Mathf.FloorToInt(Mathf.PerlinNoise((x + 100000f) * scale, (y + 100000f) * scale) * laid.Length), 0, laid.Length - 1)]))
                        plainCount++;
            // four plain pieces at twice the weight of eight featured ones: half the ground
            Assert.That(plainCount / (float)cells, Is.InRange(0.4f, 0.6f));
            foreach (var p in plain.Concat(featured)) UnityEngine.Object.DestroyImmediate(p);
        }

        [Test]
        public void WangPath_NeighboursAgreeOnEveryJoin_AndRunsMeetTheirEndsInTheUsualPlace()
        {
            var tile = ScriptableObject.CreateInstance<BellwortBurrow.Tiles.WangPathTile>();
            tile.across = Enumerable.Range(0, 9).Select(i => Named($"across_{i}")).ToArray();
            tile.up = Enumerable.Range(0, 9).Select(i => Named($"up_{i}")).ToArray();
            var go = new GameObject("grid", typeof(Grid));
            var map = new GameObject("map", typeof(Tilemap)).GetComponent<Tilemap>();
            map.transform.SetParent(go.transform);
            try
            {
                for (int x = 0; x < 40; x++) map.SetTile(new Vector3Int(x, 0, 0), tile);
                for (int y = 3; y < 43; y++) map.SetTile(new Vector3Int(60, y, 0), tile);
                int Across(int x) => System.Array.IndexOf(tile.across, map.GetSprite(new Vector3Int(x, 0, 0)));
                int Up(int y) => System.Array.IndexOf(tile.up, map.GetSprite(new Vector3Int(60, y, 0)));
                var states = new System.Collections.Generic.HashSet<int>();
                for (int x = 1; x < 39; x++)
                {
                    Assert.GreaterOrEqual(Across(x), 0, $"cell {x} of the run shows a Wang piece");
                    if (x < 38) Assert.AreEqual(Across(x) % 3, Across(x + 1) / 3, $"cells {x} and {x + 1} disagree on their join");
                    states.Add(Across(x) % 3);
                }
                Assert.AreEqual(0, Across(1) / 3, "the join next to the run's end sits where every path piece meets");
                Assert.AreEqual(0, Across(38) % 3, "and at the other end too");
                Assert.AreEqual(3, states.Count, "the joins take all three states along a long run");
                for (int y = 4; y < 41; y++)
                    Assert.AreEqual(Up(y + 1) % 3, Up(y) / 3, $"rows {y} and {y + 1} disagree on their join");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                foreach (var p in tile.across.Concat(tile.up)) UnityEngine.Object.DestroyImmediate(p);
                UnityEngine.Object.DestroyImmediate(tile);
            }
        }
    }
}
