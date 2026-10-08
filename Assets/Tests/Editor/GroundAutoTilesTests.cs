using NUnit.Framework;
using BellwortBurrow.EditorTools;

namespace BellwortBurrow.Tests.Editor
{
    public class GroundAutoTilesTests
    {
        // Sheet pieces count corners as top left 1, top right 2, bottom left 4, bottom right 8.
        // Unity's 2x2 Auto Tile counts them as bottom left 1, bottom right 2, top left 4, top right 8.
        [TestCase(1, 4u)]
        [TestCase(2, 8u)]
        [TestCase(4, 1u)]
        [TestCase(8, 2u)]
        [TestCase(3, 12u)]  // top half
        [TestCase(12, 3u)]  // bottom half
        [TestCase(5, 5u)]   // left half
        [TestCase(10, 10u)] // right half
        [TestCase(15, 15u)] // full
        public void ToAutoTileMask_MapsSheetCornersToUnityCorners(int sheetCorners, uint unityMask)
        {
            Assert.AreEqual(unityMask, GroundAutoTiles.ToAutoTileMask(sheetCorners));
        }

        [Test]
        public void ToAutoTileMask_EveryPieceGetsItsOwnMask()
        {
            var seen = new System.Collections.Generic.HashSet<uint>();
            for (int corners = 0; corners < 16; corners++)
                Assert.IsTrue(seen.Add(GroundAutoTiles.ToAutoTileMask(corners)), $"piece {corners} shares a mask");
        }
    }
}
