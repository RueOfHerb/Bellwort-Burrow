using BellwortBurrow.Systems.World;
using NUnit.Framework;
using UnityEngine;

namespace BellwortBurrow.Tests.EditMode
{
    public class FrontLineTests
    {
        // A stump's front: its middle on its feet, curving back 2 units at either side.
        static readonly Vector2[] Stump = { new(-6f, 2f), new(-4f, 0f), new(4f, 0f), new(6f, 2f) };

        [Test]
        public void InFrontOfACurvedSide_DrawsOverThePiece()
        {
            Assert.IsTrue(FrontLine.InFront(Stump, new Vector2(5.5f, 1f)), "in front of the root at the side");
        }

        [Test]
        public void BehindTheLine_OrBelowTheFeet_OrPastTheSides_LeavesSortingAlone()
        {
            Assert.IsFalse(FrontLine.InFront(Stump, new Vector2(5.5f, 2f)), "behind the line");
            Assert.IsFalse(FrontLine.InFront(Stump, new Vector2(0f, -1f)), "in front of the feet: sorting already right");
            Assert.IsFalse(FrontLine.InFront(Stump, new Vector2(7f, 1f)), "past the side");
        }

        [Test]
        public void BehindABridge_IsNorthOfItsFarRail()
        {
            var far = new[] { new Vector2(-7f, 0.5f), new Vector2(0f, 1.75f), new Vector2(7f, 0.5f) };
            Assert.IsTrue(Bridge.IsBehind(far, new Vector2(-6f, 1.5f), -7.5f, 7.5f));
            Assert.IsTrue(Bridge.IsBehind(far, new Vector2(-7.2f, 1f), -7.5f, 7.5f), "by the end pillar, past the rail's end");
            Assert.IsFalse(Bridge.IsBehind(far, new Vector2(0f, 1f), -7.5f, 7.5f), "on the deck's line or in front of it");
            Assert.IsFalse(Bridge.IsBehind(far, new Vector2(8f, 3f), -7.5f, 7.5f), "past the bridge");
        }
    }
}
