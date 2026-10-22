using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;
using BellwortBurrow.EditorTools;
using BellwortBurrow.Tiles;

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

        [Test]
        public void FullLayout_AmongFullTilesIsTheLayout()
        {
            foreach (var around in EveryNeighbourhood())
            {
                var types = new char[8];
                for (int i = 0; i < 8; i++) types[i] = (around & RuleOrder[i]) != 0 ? 'F' : '.';
                Assert.AreEqual(GroundAutoTiles.LayoutOf(around), GroundAutoTiles.FullLayout(types), $"neighbourhood {around}");
            }
        }

        [TestCase('.', '.', 'F', '.', 1 | 4)]       // full path to the west: the left half
        [TestCase('F', '.', '.', '.', 1 | 2)]       // to the north: the top half
        [TestCase('F', '.', '.', 'F', 1 | 2 | 8)]   // north and east: all but the bottom left
        [TestCase('.', '.', 'F', 'F', 15)]          // both sides: all of it
        [TestCase('H', 'H', 'H', 'H', 0)]           // only half path beside it: a lone dot
        public void Quarters_HalfTurnsTowardFullPath(char n, char s, char w, char e, int expected)
        {
            Assert.AreEqual(expected, GroundAutoTiles.Quarters('H', n, s, w, e));
        }

        [Test]
        public void HalfTemplate_HoldsEveryLook()
        {
            var t = GroundAutoTiles.HalfTemplate;
            for (int row = 0; row < t.Length; row++)
                Assert.AreEqual(t[0].Length, t[row].Length, $"row {row} is a different length");
            var every = new HashSet<string>();
            for (int i = 0; i < GroundAutoTiles.Neighbourhoods; i++) every.Add(GroundAutoTiles.HalfLook(GroundAutoTiles.NeighbourhoodOf(i)));
            Assert.AreEqual(GroundAutoTiles.HalfLookCount, every.Count);
            var have = new HashSet<string>();
            for (int row = 0; row < t.Length; row++)
                for (int column = 0; column < t[row].Length; column++)
                    if (t[row][column] == 'H') have.Add(GroundAutoTiles.HalfLook(GroundAutoTiles.Around(t, column, row)));
            foreach (var look in every) Assert.IsTrue(have.Contains(look), $"the half template lacks look {look}");
        }

        static void EveryNeighbourhoodMatchesOnlyRulesForItsPiece<T>(Func<char[], T> keyOf)
        {
            var rules = GroundAutoTiles.PathRules(keyOf);
            var lets = rules.Select(r => r.conditions.Select(GroundAutoTiles.TypesOf).ToArray()).ToArray();
            for (int i = 0; i < GroundAutoTiles.Neighbourhoods; i++)
            {
                var around = GroundAutoTiles.NeighbourhoodOf(i);
                int matched = 0;
                for (int r = 0; r < lets.Length; r++)
                {
                    bool ok = true;
                    for (int k = 0; k < 8 && ok; k++) ok = lets[r][k].IndexOf(around[k]) >= 0;
                    if (!ok) continue;
                    matched++;
                    // rules may overlap, but only rules with the same piece
                    Assert.AreEqual(keyOf(around), rules[r].key, $"neighbourhood {new string(around)} matches a rule for another piece");
                }
                Assert.Greater(matched, 0, $"neighbourhood {new string(around)} matches no rule");
            }
        }

        [Test]
        public void PathRules_EveryNeighbourhoodGetsItsOwnPiece()
        {
            EveryNeighbourhoodMatchesOnlyRulesForItsPiece<uint>(GroundAutoTiles.FullLayout);
            EveryNeighbourhoodMatchesOnlyRulesForItsPiece<string>(GroundAutoTiles.HalfLook);
        }

        [Test]
        public void PathRuleTile_TellsFullHalfAndNoPathApart()
        {
            var full = ScriptableObject.CreateInstance<PathRuleTile>();
            var half = ScriptableObject.CreateInstance<PathRuleTile>();
            var other = ScriptableObject.CreateInstance<Tile>();
            full.fullPath = half.fullPath = full;
            full.halfPath = half.halfPath = half;
            foreach (var (condition, types) in new[] { (0, "FH."), (3, "F"), (4, "H"), (5, "."), (6, "FH"), (7, "H."), (8, "F.") })
            {
                Assert.AreEqual(types, GroundAutoTiles.TypesOf(condition));
                Assert.AreEqual(types.Contains('F'), condition == 0 || full.RuleMatch(condition, full), $"condition {condition} on full path");
                Assert.AreEqual(types.Contains('H'), condition == 0 || full.RuleMatch(condition, half), $"condition {condition} on half path");
                Assert.AreEqual(types.Contains('.'), condition == 0 || full.RuleMatch(condition, other), $"condition {condition} on another tile");
                Assert.AreEqual(types.Contains('.'), condition == 0 || full.RuleMatch(condition, null), $"condition {condition} on an empty cell");
            }
            UnityEngine.Object.DestroyImmediate(full);
            UnityEngine.Object.DestroyImmediate(half);
            UnityEngine.Object.DestroyImmediate(other);
        }

        [Test]
        public void EveryGroundSheet_MakesGrassAndAFullAndHalfPathAndDeerTrail()
        {
            var every = new HashSet<string>();
            foreach (var ground in GroundAutoTiles.Sheets)
            {
                var names = GroundAutoTiles.TilePaths(ground).Select(System.IO.Path.GetFileNameWithoutExtension).ToList();
                var kinds = new[] { "Grass", "Path", "Path Half", "Deer Trail", "Deer Trail Half" }.ToList();
                if (ground.Stone) kinds.AddRange(new[] { "Flagstones", "Flagstones Half" });
                if (ground.Water) kinds.AddRange(new[] { "Water", "Water Half" });
                if (ground.Banks) kinds.AddRange(new[] { "Bank Face", "Cliff Face" });
                CollectionAssert.AreEqual(kinds.Select(k => ground.Zone + " " + k), names, ground.Sheet);
                foreach (var path in GroundAutoTiles.TilePaths(ground))
                    Assert.IsTrue(every.Add(path), $"{path} comes from two ground sheets");
            }
            Assert.AreEqual(GroundAutoTiles.Sheets.Length, GroundAutoTiles.Sheets.Select(g => g.Palette).Distinct().Count(), "two ground sheets share a palette");
        }
    }
}
