using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
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
        public void PieceName_NumbersTilesLikeTheImporter()
        {
            Assert.AreEqual("path_set_000", GroundAutoTiles.PieceName("path_set", 0, 0, GroundAutoTiles.Template));
            Assert.AreEqual("path_set_018", GroundAutoTiles.PieceName("path_set", 1, 1, GroundAutoTiles.Template));
        }
    }
}
