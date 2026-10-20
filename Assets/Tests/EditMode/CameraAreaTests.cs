using BellwortBurrow.Player;
using BellwortBurrow.Systems.World;
using NUnit.Framework;
using UnityEngine;

namespace BellwortBurrow.Tests.EditMode
{
    public class CameraAreaTests
    {
        CameraArea leg, band;

        [SetUp]
        public void MakeAnL()
        {
            leg = new GameObject("Leg").AddComponent<CameraArea>();
            leg.standing = new Rect(0f, 0f, 30f, 24f);
            leg.view = new Rect(0f, 0f, 30f, 32f);
            band = new GameObject("Band").AddComponent<CameraArea>();
            band.standing = new Rect(0f, 24f, 72f, 17f);
            band.view = new Rect(0f, 24f, 72f, 17f);
        }

        [TearDown]
        public void CleanUp()
        {
            Object.DestroyImmediate(leg.gameObject);
            Object.DestroyImmediate(band.gameObject);
        }

        [Test]
        public void Pick_TheAreaThePlayerStandsIn()
        {
            var areas = new[] { leg, band };
            Assert.AreSame(leg, CameraArea.Pick(areas, new Vector2(8f, 3f), null));
            Assert.AreSame(band, CameraArea.Pick(areas, new Vector2(8f, 26f), leg), "stepping round the corner");
            Assert.AreSame(leg, CameraArea.Pick(areas, new Vector2(8f, 20f), band), "and back down");
        }

        [Test]
        public void Pick_InTwoAtOnce_KeepsTheOneTheyWereIn()
        {
            band.standing = new Rect(0f, 20f, 72f, 21f);           // overlaps the leg from y 20 to 24
            var areas = new[] { leg, band };
            Assert.AreSame(leg, CameraArea.Pick(areas, new Vector2(8f, 22f), leg));
            Assert.AreSame(band, CameraArea.Pick(areas, new Vector2(8f, 22f), band));
        }

        [Test]
        public void Pick_OutsideEveryArea_KeepsTheLastOne()
        {
            Assert.AreSame(band, CameraArea.Pick(new[] { leg, band }, new Vector2(80f, 50f), band));
            Assert.IsNull(CameraArea.Pick(new CameraArea[0], new Vector2(1f, 1f), null));
        }

        [Test]
        public void Clamp_KeepsTheScreenInsideTheView()
        {
            // a 30 by 16.875 unit screen, the reference 480 by 270 pixels
            var c = CameraFollow.Clamp(new Vector2(8f, 27f), band.view, 15f, 8.4375f);
            Assert.AreEqual(15f, c.x, 1e-5f, "held off the west edge");
            Assert.AreEqual(24f + 8.4375f, c.y, 1e-5f, "the band sits at the bottom, the sky above it");
            c = CameraFollow.Clamp(new Vector2(40f, 30f), band.view, 15f, 8.4375f);
            Assert.AreEqual(40f, c.x, 1e-5f, "follows across the middle");
        }

        [Test]
        public void Clamp_AViewSmallerThanTheScreen_IsCentred()
        {
            var c = CameraFollow.Clamp(new Vector2(3f, 3f), new Rect(0f, 0f, 20f, 10f), 15f, 8.4375f);
            Assert.AreEqual(new Vector2(10f, 5f), c);
        }
    }
}
