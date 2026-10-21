using BellwortBurrow.Systems.World;
using NUnit.Framework;
using UnityEngine;

namespace BellwortBurrow.Tests.EditMode
{
    public class ParallaxTests
    {
        static readonly Vector3 Placed = new Vector3(40f, 10f, 0f);

        [Test]
        public void SitsWherePlaced_WhileTheCameraIsCentredOnIt()
        {
            Assert.AreEqual(Placed, Parallax.Drifted(Placed, new Vector3(40f, 10f, -10f), new Vector2(0.5f, 0.5f), 16));
        }

        [Test]
        public void FollowZero_StaysPutLikeTheGround()
        {
            Assert.AreEqual(Placed, Parallax.Drifted(Placed, new Vector3(12f, 3f, -10f), Vector2.zero, 16));
        }

        [Test]
        public void FollowOne_MovesWithTheCameraLikeTheSky()
        {
            var a = Parallax.Drifted(Placed, new Vector3(30f, 10f, -10f), Vector2.one, 16);
            var b = Parallax.Drifted(Placed, new Vector3(50f, 10f, -10f), Vector2.one, 16);
            Assert.AreEqual(20f, b.x - a.x, 1e-5f);
        }

        [Test]
        public void FollowHalf_DriftsAcrossTheScreenAtHalfTheGroundsSpeed()
        {
            var cameraFrom = new Vector3(30f, 10f, -10f);
            var cameraTo = new Vector3(38f, 10f, -10f);
            var from = Parallax.Drifted(Placed, cameraFrom, new Vector2(0.5f, 0f), 16);
            var to = Parallax.Drifted(Placed, cameraTo, new Vector2(0.5f, 0f), 16);
            float groundOnScreen = -(cameraTo.x - cameraFrom.x);
            float backdropOnScreen = (to.x - cameraTo.x) - (from.x - cameraFrom.x);
            Assert.AreEqual(groundOnScreen * 0.5f, backdropOnScreen, 1e-5f);
        }

        [Test]
        public void UpAndDownKeepsItsOwnShare()
        {
            var p = Parallax.Drifted(Placed, new Vector3(40f, 18f, -10f), new Vector2(0.5f, 0f), 16);
            Assert.AreEqual(Placed.y, p.y, 1e-5f, "follow 0 up and down stays put");
            p = Parallax.Drifted(Placed, new Vector3(40f, 18f, -10f), new Vector2(0.5f, 0.25f), 16);
            Assert.AreEqual(Placed.y + 2f, p.y, 1e-5f);
        }

        [Test]
        public void SnapsToWholePixels()
        {
            var p = Parallax.Drifted(Placed, new Vector3(40.37f, 10.11f, -10f), new Vector2(0.33f, 0.33f), 16);
            Assert.AreEqual(Mathf.Round(p.x * 16f), p.x * 16f, 1e-4f);
            Assert.AreEqual(Mathf.Round(p.y * 16f), p.y * 16f, 1e-4f);
            Assert.AreEqual(Placed.z, p.z);
        }
    }
}
