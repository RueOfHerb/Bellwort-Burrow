using BellwortBurrow.Systems.World;
using NUnit.Framework;
using UnityEngine;

namespace BellwortBurrow.Tests.EditMode
{
    public class WallPieceTests
    {
        [Test]
        public void Pick_TheWallItWasDroppedOn_TheNearestMiddleWhereTwoOverlap()
        {
            var walls = new[] { new Rect(0f, 0f, 50f, 40f), new Rect(45f, 0f, 10f, 10f) };
            Assert.AreEqual(0, WallPiece.Pick(new Vector2(20f, 30f), walls));
            Assert.AreEqual(1, WallPiece.Pick(new Vector2(49f, 4f), walls), "on both: the small one's middle is nearer");
            Assert.AreEqual(-1, WallPiece.Pick(new Vector2(70f, 5f), walls), "on neither");
        }

        [Test]
        public void Attach_PutsThePieceOnTheBuildingAndKeepsItsSpot()
        {
            var texture = new Texture2D(32, 32);
            var stump = new GameObject("burrow_stump");
            var picture = stump.AddComponent<SpriteRenderer>();
            picture.sprite = Sprite.Create(texture, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0f), 16f);
            stump.AddComponent<WallSeat>();
            stump.transform.position = new Vector3(10f, 5f, 0f);
            var ladder = new GameObject("burrow_stump_ladder").AddComponent<WallPiece>();
            ladder.transform.position = new Vector3(9.5f, 5.5f, 0f);
            try
            {
                Assert.IsTrue(ladder.Attach());
                Assert.AreSame(stump.transform, ladder.transform.parent);
                Assert.AreEqual(new Vector3(9.5f, 5.5f, 0f), ladder.transform.position, "it stays where it was dropped");
                Assert.IsFalse(ladder.Attach(), "already on its wall");
            }
            finally
            {
                Object.DestroyImmediate(ladder.gameObject);
                Object.DestroyImmediate(stump);
                Object.DestroyImmediate(texture);
            }
        }
    }
}
