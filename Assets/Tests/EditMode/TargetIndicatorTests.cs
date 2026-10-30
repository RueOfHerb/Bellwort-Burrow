using NUnit.Framework;
using UnityEngine;
using BellwortBurrow.Player;

namespace BellwortBurrow.Tests.EditMode
{
    /// <summary>The target indicator and the mouse's pick of a tile round the player (ui-5).</summary>
    public class TargetIndicatorTests
    {
        [Test]
        public void TheMousePicksOnlyTheTilesRoundThePlayer()
        {
            var feet = new Vector2(10.4f, 5.9f);   // standing on tile (10, 5)
            Assert.IsTrue(PlayerInteractor.TileNear(feet, new Vector2(11.9f, 6.2f), 1, out var middle));
            Assert.AreEqual(new Vector2(11.5f, 6.5f), middle, "the middle of the tile under the pointer");
            Assert.IsTrue(PlayerInteractor.TileNear(feet, new Vector2(9.1f, 4.0f), 1, out _), "diagonal");
            Assert.IsTrue(PlayerInteractor.TileNear(feet, new Vector2(10.5f, 5.5f), 1, out _), "their own tile");
            Assert.IsFalse(PlayerInteractor.TileNear(feet, new Vector2(12.1f, 5.5f), 1, out _), "two tiles away");
            Assert.IsFalse(PlayerInteractor.TileNear(feet, new Vector2(10.5f, 3.9f), 1, out _), "two tiles down");
        }

        [Test]
        public void TheBracketsSitOnThePixelGrid()
        {
            var tile = new Bounds(new Vector3(3.5f, 2.5f), Vector3.one);
            var (min, max) = TargetIndicator.Snap(tile, 0);
            Assert.AreEqual(new Vector2(3f, 2f), min);
            Assert.AreEqual(new Vector2(4f, 3f), max);
            var clump = new Bounds(new Vector3(1.03f, 1.01f), new Vector3(0.5f, 0.4f));
            (min, max) = TargetIndicator.Snap(clump, 1);
            Assert.AreEqual(Mathf.Floor(0.78f * 16) / 16 - 1 / 16f, min.x, 1e-5f, "a pixel out from the picture");
            Assert.AreEqual(Mathf.Ceil(1.28f * 16) / 16 + 1 / 16f, max.x, 1e-5f);
        }
    }
}
