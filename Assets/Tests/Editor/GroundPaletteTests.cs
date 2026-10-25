using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using BellwortBurrow.EditorTools;

namespace BellwortBurrow.Tests.Editor
{
    public class GroundPaletteTests
    {
        // Neighbour bits, y up (top row 64 128 256, middle 8 16 32, bottom 1 2 4), by step in the palette, rows down.
        static readonly (int dx, int dy, uint bit)[] Neighbours =
        {
            (-1, -1, 64), (0, -1, 128), (1, -1, 256), (-1, 0, 8), (1, 0, 32), (-1, 1, 1), (0, 1, 2), (1, 1, 4),
        };

        static readonly string[] PathFills = { "path_fill_00", "path_fill_01" };

        static Dictionary<string, uint> TemplatePieces(string slice)
        {
            var t = GroundAutoTiles.Template;
            var pieces = new Dictionary<string, uint>();
            for (int row = 0; row < t.Length; row++)
                for (int column = 0; column < t[row].Length; column++)
                    if (t[row][column] == '#') pieces[GroundAutoTiles.PieceName(slice, column, row, t)] = GroundAutoTiles.Layout(t, column, row);
            return pieces;
        }

        [Test]
        public void Shapes_HoldEveryLayout()
        {
            var want = new HashSet<uint>(TemplatePieces("path_set").Values);
            var have = new HashSet<uint>(GroundPalette.ShapeSpots().Select(s => s.layout));
            Assert.AreEqual(GroundAutoTiles.LayoutCount, have.Count);
            CollectionAssert.AreEquivalent(want, have);
        }

        [Test]
        public void Shapes_OnlyJoinTheirOwnPieces()
        {
            // Every piece's layout, read from all the shapes together, is the one its own shape gives it.
            var spots = GroundPalette.ShapeSpots();
            var cells = new HashSet<(int, int)>(spots.Select(s => (s.column, s.row)));
            Assert.AreEqual(spots.Count, cells.Count, "two shapes share a cell");
            foreach (var (column, row, layout) in spots)
            {
                uint around = 16;
                foreach (var (dx, dy, bit) in Neighbours)
                    if (cells.Contains((column + dx, row + dy))) around |= bit;
                Assert.AreEqual(layout, GroundAutoTiles.LayoutOf(around), $"the piece at column {column}, row {row}");
            }
        }

        [Test]
        public void SetSpots_HoldEveryPieceOnceWithItsOwnLayout()
        {
            var pieces = TemplatePieces("path_set");
            foreach (var fill in PathFills) pieces[fill] = GroundAutoTiles.Full;
            var spots = GroundPalette.SetSpots("path_set", PathFills);

            CollectionAssert.AreEquivalent(pieces.Keys, spots.Select(s => s.piece).Distinct());
            Assert.AreEqual(spots.Count, spots.Select(s => (s.column, s.row)).Distinct().Count(), "two pieces share a cell");
            Assert.IsTrue(spots.All(s => s.column >= 0 && s.column < GroundPalette.SetWidth && s.row >= 0),
                "a piece sits outside its set's columns");

            // Each shape cell holds a piece of the layout the shape needs there.
            var at = spots.ToDictionary(s => (s.column, s.row), s => s.piece);
            foreach (var (column, row, layout) in GroundPalette.ShapeSpots())
                Assert.AreEqual(layout, pieces[at[(column, row)]], at[(column, row)]);

            // Besides the shapes, each piece comes up once.
            int shapeCount = GroundPalette.ShapeSpots().Count;
            Assert.AreEqual(shapeCount + pieces.Count - GroundAutoTiles.LayoutCount, spots.Count);
        }

        [Test]
        public void SplitRuns_KeepsTheShapesAndGivesTheRunsAColumnOfTheirOwn()
        {
            // The full set: the shape cells stay where they are, everything else is a run, from row 0 down.
            var spots = GroundPalette.SetSpots("path_set", PathFills);
            var (shapes, runs) = GroundPalette.SplitRuns(spots, GroundPalette.ShapeSpots().Max(s => s.row));
            CollectionAssert.AreEquivalent(GroundPalette.ShapeSpots().Select(s => (s.column, s.row)), shapes.Select(s => (s.column, s.row)));
            Assert.AreEqual(spots.Count, shapes.Count + runs.Count);
            Assert.AreEqual(0, runs.Min(s => s.row), "the runs start on their column's first row");
            Assert.AreEqual(runs.Count, runs.Select(s => (s.column, s.row)).Distinct().Count(), "two runs share a cell");
        }
    }
}
