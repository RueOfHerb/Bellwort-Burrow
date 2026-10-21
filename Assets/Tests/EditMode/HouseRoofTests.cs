using BellwortBurrow.Systems.World;
using NUnit.Framework;
using UnityEngine;

namespace BellwortBurrow.Tests.EditMode
{
    public class HouseRoofTests
    {
        [Test]
        public void Nearest_PicksTheClosestHouseWithinReach()
        {
            var houses = new[] { new Vector2(0f, 0f), new Vector2(6f, 0f), new Vector2(6.5f, 2f) };
            Assert.AreEqual(1, HouseRoof.Nearest(new Vector2(5.2f, 0.4f), houses, HouseRoof.Reach));
            Assert.AreEqual(0, HouseRoof.Nearest(new Vector2(0.5f, -0.5f), houses, HouseRoof.Reach));
            Assert.AreEqual(-1, HouseRoof.Nearest(new Vector2(20f, 0f), houses, HouseRoof.Reach), "too far from every house");
            Assert.AreEqual(-1, HouseRoof.Nearest(Vector2.zero, new Vector2[0], HouseRoof.Reach));
        }

        [Test]
        public void Attach_PutsTheRoofOnTheHouseOfItsSize()
        {
            var nook = new GameObject("burrow_nook_red").AddComponent<RoofSeat>();
            nook.size = "burrow_nook";
            nook.transform.position = new Vector3(3f, 2f, 0f);
            var home = new GameObject("burrow_home_red").AddComponent<RoofSeat>();
            home.size = "burrow_home";
            home.transform.position = new Vector3(4f, 2f, 0f);
            var roof = new GameObject("burrow_home_roof_red").AddComponent<HouseRoof>();
            roof.fits = "burrow_home";
            roof.transform.position = new Vector3(3.1f, 2.2f, 0f);
            try
            {
                Assert.IsTrue(roof.Attach());
                Assert.AreSame(home.transform, roof.transform.parent, "the nook is nearer, but a home roof only fits a home");
                Assert.AreEqual(Vector3.zero, roof.transform.localPosition);
                Assert.IsFalse(roof.Attach(), "already on its house");
            }
            finally
            {
                Object.DestroyImmediate(roof.gameObject);
                Object.DestroyImmediate(home.gameObject);
                Object.DestroyImmediate(nook.gameObject);
            }
        }
    }
}
