using BellwortBurrow.Systems.World;
using NUnit.Framework;
using UnityEngine;

namespace BellwortBurrow.Tests.EditMode
{
    public class SeeThroughTests
    {
        // A beam 4 units wide and 28 tall on a support whose feet are at y 10.
        static readonly Rect Beam = new Rect(8f, 13f, 4f, 28f);

        [Test]
        public void BehindTheBeam_TurnsItSeeThrough()
        {
            Assert.IsTrue(SeeThrough.IsBehind(new Vector2(10f, 14f), 10f, Beam, 0.75f));
            Assert.IsTrue(SeeThrough.IsBehind(new Vector2(12.5f, 30f), 10f, Beam, 0.75f), "half the player still behind its edge");
        }

        [Test]
        public void JustBehindIt_ItWaitsTheTilesBeforeFading()
        {
            Assert.IsFalse(SeeThrough.IsBehind(new Vector2(10f, 11.5f), 10f, Beam, 0.75f, 2f), "a tile and a half in: still solid");
            Assert.IsTrue(SeeThrough.IsBehind(new Vector2(10f, 12.5f), 10f, Beam, 0.75f, 2f), "past two tiles: it fades");
        }

        [Test]
        public void InFront_ToTheSide_OrAboveIt_LeavesItSolid()
        {
            Assert.IsFalse(SeeThrough.IsBehind(new Vector2(10f, 9f), 10f, Beam, 0.75f), "in front of the support");
            Assert.IsFalse(SeeThrough.IsBehind(new Vector2(14f, 20f), 10f, Beam, 0.75f), "clear of its side");
            Assert.IsFalse(SeeThrough.IsBehind(new Vector2(10f, 42f), 10f, Beam, 0.75f), "past its top");
        }
    }
}
