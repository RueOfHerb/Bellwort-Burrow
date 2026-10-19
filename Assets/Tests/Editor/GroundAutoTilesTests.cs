using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;
using BellwortBurrow.EditorTools;

namespace BellwortBurrow.Tests.Editor
{
    public class GroundAutoTilesTests
    {
        // Unity's 3x3 Auto Tile bits (y up): top row 64 128 256, middle 8 16 32, bottom 1 2 4.
        [TestCase(new[] { "...", ".#.", "..." }, 16u)]                        // on its own
        [TestCase(new[] { "###", "###", "###" }, 511u)]                       // surrounded
        [TestCase(new[] { "...", "###", "..." }, 8u + 16 + 32)]               // the middle of a one tile path
        [TestCase(new[] { "#.#", ".#.", "#.#" }, 16u)]                        // corners alone don't count
        [TestCase(new[] { "##.", "##.", "..." }, 64u + 128 + 8 + 16)]         // bottom right of a block
        [TestCase(new[] { ".#.", "###", ".#." }, 2u + 8 + 16 + 32 + 128)]    // a crossroads
        public void Layout_ReadsNeighboursLikeUnity(string[] grid, uint expected)
        {
            Assert.AreEqual(expected, GroundAutoTiles.Layout(grid, 1, 1));
        }

        [Test]
        public void Template_HoldsAll47Layouts()
        {
            var t = GroundAutoTiles.Template;
            var seen = new HashSet<uint>();
            for (int row = 0; row < t.Length; row++)
            {
                Assert.AreEqual(t[0].Length, t[row].Length, $"row {row} is a different length");
                for (int column = 0; column < t[row].Length; column++)
                    if (t[row][column] == '#') seen.Add(GroundAutoTiles.Layout(t, column, row));
            }
            Assert.AreEqual(47, seen.Count);
            Assert.IsTrue(seen.Contains(GroundAutoTiles.Full));
        }

        [Test]
        public void Template_HasAPieceForEveryNeighbourhoodUnityCanSee()
        {
            // Unity folds the 256 ways a tile's neighbours can be painted into a layout with a private method;
            // every one of those layouts must be in the template, or that spot would show the default piece.
            var convert = typeof(AutoTile).GetMethod("Convert3x3Mask", BindingFlags.NonPublic | BindingFlags.Instance);
            if (convert == null) Assert.Ignore("Tilemap Extras no longer has Convert3x3Mask; check the layouts by hand.");
            var tile = ScriptableObject.CreateInstance<AutoTile>();
            try
            {
                var t = GroundAutoTiles.Template;
                var have = new HashSet<uint>();
                for (int row = 0; row < t.Length; row++)
                    for (int column = 0; column < t[row].Length; column++)
                        if (t[row][column] == '#') have.Add(GroundAutoTiles.Layout(t, column, row));

                uint[] around = { 1, 2, 4, 8, 32, 64, 128, 256 };
                for (int i = 0; i < 256; i++)
                {
                    uint mask = 16;
                    for (int b = 0; b < 8; b++) if ((i & (1 << b)) != 0) mask |= around[b];
                    uint layout = (uint)convert.Invoke(tile, new object[] { mask });
                    Assert.IsTrue(have.Contains(layout), $"neighbourhood {mask} becomes layout {layout}, which the template lacks");
                }
            }
            finally { Object.DestroyImmediate(tile); }
        }

        [Test]
        public void PieceName_NumbersTilesLikeTheImporter()
        {
            Assert.AreEqual("path_set_000", GroundAutoTiles.PieceName("path_set", 0, 0, GroundAutoTiles.Template));
            Assert.AreEqual("path_set_018", GroundAutoTiles.PieceName("path_set", 1, 1, GroundAutoTiles.Template));
        }
    }
}
