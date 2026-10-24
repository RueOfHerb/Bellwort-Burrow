using System.Linq;
using BellwortBurrow.EditorTools;
using BellwortBurrow.Systems.World;
using NUnit.Framework;
using UnityEngine;

namespace BellwortBurrow.Tests.Editor
{
    public class BridgesTests
    {
        static Bridges.DiagonalDeck Trestle => (Bridges.DiagonalDeck)Bridges.Shapes["burrow_bridge_trestle"];
        static Bridges.StraightDeck Straight => (Bridges.StraightDeck)Bridges.Shapes["burrow_bridge_straight"];

        [Test]
        public void TheTrestleIsABridge_ItsRailsGoOnIt()
        {
            Assert.IsTrue(Bridges.IsBridge("burrow_bridge_trestle"));
            Assert.IsFalse(Bridges.IsBridge("burrow_bridge_trestle_near_rail"));
            Assert.IsFalse(Bridges.IsBridge("burrow_home_red"));

            Assert.IsTrue(Bridges.IsRail("burrow_bridge_trestle_near_rail", out var near));
            Assert.AreEqual("burrow_bridge_trestle", near);
            Assert.IsTrue(Bridges.IsRail("burrow_bridge_trestle_far_rail", out var far));
            Assert.AreEqual("burrow_bridge_trestle", far);
            Assert.IsFalse(Bridges.IsRail("burrow_bridge_trestle", out _));
            Assert.IsFalse(Bridges.IsRail("burrow_somewhere_near_rail", out _), "only a known bridge has rails");

            var names = new[] { "burrow_bridge_trestle", "burrow_bridge_trestle_near_rail", "burrow_bridge_trestle_far_rail" };
            Assert.IsTrue(Footprints.IsPartOfABase("burrow_bridge_trestle_near_rail", names), "the rails go in the bridge's prefab");
            Assert.IsTrue(Footprints.IsPartOfABase("burrow_bridge_trestle_far_rail", names));
            Assert.IsFalse(Footprints.IsPartOfABase("burrow_bridge_trestle", names));
            Assert.IsFalse(Colliders.IsSolid("burrow_bridge_trestle"), "a bridge gets its own colliders, not a footprint");
        }

        [Test]
        public void TheDeckIsLevelOverTheWater_AndRampsToTheGroundAtTheEnds()
        {
            Assert.AreEqual(13f, Trestle.DeckHeight(0f));
            Assert.AreEqual(13f, Trestle.DeckHeight(30f));
            Assert.AreEqual(13f, Trestle.DeckHeight(-30f));
            Assert.AreEqual(6.5f, Trestle.DeckHeight(39f), 1e-4f);
            Assert.AreEqual(0f, Trestle.DeckHeight(48f));
            Assert.AreEqual(0f, Trestle.DeckHeight(-48f));
        }

        [Test]
        public void TheShapeFitsInsideTheSlice_AboveItsFeet()
        {
            // The slice is 117 by 131 with its feet at 58, 131: x from -58 to 59, y from 0 to 131 around the feet.
            var shape = Trestle;
            var all = shape.Deck().Concat(shape.NearSide(Bridges.NearInset, Bridges.EndGap))
                .Concat(shape.FarSide(Bridges.FarInset, Bridges.EndGap));
            foreach (var p in all)
            {
                Assert.That(p.x, Is.InRange(-58f, 59f), $"{p} sits outside the slice");
                Assert.That(p.y, Is.InRange(0f, 131f), $"{p} sits outside the slice");
            }
        }

        [Test]
        public void ThePlayerIsKeptOnTheDeck_AndTheEndsAreOpen()
        {
            var shape = Trestle;
            var deck = shape.Deck();
            var near = shape.NearSide(Bridges.NearInset, Bridges.EndGap);
            var far = shape.FarSide(Bridges.FarInset, Bridges.EndGap);

            // The line the player's feet stop at on each side is on the deck.
            foreach (var p in near.Take(4).Concat(far))
                Assert.IsTrue(Inside(deck, p), $"{p} is off the deck");

            // The walkway (the trigger) stays inside both side lines, so pressing against a side from outside never puts
            // a player on the bridge; and the side lines stay off it.
            var walkway = shape.Walkway(Bridges.NearInset, Bridges.FarInset, Bridges.WalkwayGap);
            foreach (int i in new[] { 1, 2, 5, 6 }) Assert.IsTrue(Inside(deck, walkway[i]), $"{walkway[i]} is off the deck");
            foreach (var p in near.Take(4).Concat(far)) Assert.IsFalse(Inside(walkway, p), $"{p} is on the walkway");
            for (float k = -40f; k <= 40f; k += 4f)
                Assert.IsTrue(Inside(walkway, shape.At(k, -1f, shape.DeckHeight(k))), $"the middle of the deck at k {k}");

            // Between the sides' ends there's room for the player (12 px wide) to walk on and off.
            Assert.Greater(Vector2.Distance(near[0], far[0]), 12f, "the northwest end is open");
            Assert.Greater(Vector2.Distance(near[3], far[3]), 12f, "the southeast end is open");

        }

        [Test]
        public void AlongTheDeck_TheNearRailStandsInFrontOfThePlayer_AndTheFarRailBehind()
        {
            var shape = Trestle;
            var nearFoot = shape.RailFoot(-shape.HalfWidth + Bridges.RailInset);
            var farFoot = shape.RailFoot(shape.HalfWidth - Bridges.RailInset);
            for (float k = -shape.Length + Bridges.EndGap; k <= shape.Length - Bridges.EndGap; k += 0.5f)
            {
                // Where the player's feet stop on each side (the capsule keeps them a few more pixels in).
                var nearest = shape.At(k, -shape.HalfWidth + Bridges.NearInset, shape.DeckHeight(k));
                var farthest = shape.At(k, shape.HalfWidth - Bridges.FarInset, shape.DeckHeight(k));
                // Further back (higher) than the near rail's foot under them, so the near rail draws over them. The line
                // is at least 1 px behind the rail (a little less than 2 on the ramps, where the deck tilts), the
                // player's capsule keeps their feet about 4 px further in, and a strip sorts within half its width
                // (2 px) of the rail's line, so the near rail always sorts in front.
                float behind = nearest.y - Bridge.HeightAt(nearFoot, nearest.x);
                Assert.Greater(behind + 4f - Bridges.StripWidth / 2f, 0f, $"near side at k {k}");
                Assert.GreaterOrEqual(behind, 1f, $"near side at k {k}");
                // ...and nearer (lower) than the far rail's foot, so they draw over the far rail.
                Assert.Greater(Bridge.HeightAt(farFoot, farthest.x) - farthest.y, 0f, $"far side at k {k}");
            }
        }

        [Test]
        public void StripsAreTheColumnsWithPixels()
        {
            // A 12 by 2 sheet with one slice over all of it: pixels in columns 1 and 9 (art) and 6 (glow).
            int w = 12, h = 2;
            var art = new byte[w * h * 4];
            var glow = new byte[w * h * 4];
            art[(0 * w + 1) * 4 + 3] = 255;
            art[(1 * w + 9) * 4 + 3] = 255;
            glow[(1 * w + 6) * 4 + 3] = 255;
            var slice = new SheetSlice { Name = "rail", X = 0, Y = 0, Width = w, Height = h };
            CollectionAssert.AreEqual(new[] { 0, 4, 8 }, Bridges.Strips(art, glow, w, h, slice, 4));
            CollectionAssert.AreEqual(new[] { 0, 8 }, Bridges.Strips(art, null, w, h, slice, 4));
        }

        [Test]
        public void TheStraightBridgeIsABridge_ItsRailsGoOnIt()
        {
            Assert.IsTrue(Bridges.IsBridge("burrow_bridge_straight"));
            Assert.IsTrue(Bridges.IsRail("burrow_bridge_straight_near_rail", out var near));
            Assert.AreEqual("burrow_bridge_straight", near);
            Assert.IsTrue(Bridges.IsRail("burrow_bridge_straight_far_rail", out var far));
            Assert.AreEqual("burrow_bridge_straight", far);
        }

        [Test]
        public void TheStraightBridge_FitsItsSlice_KeepsThePlayerOnTheDeck_AndSortsTheRails()
        {
            // The slice is 196 by 79 with its feet at 98, 79: x from -98 to 98, y from 0 to 79 around the feet.
            var shape = Straight;
            foreach (var p in shape.Deck().Concat(shape.NearSide()).Concat(shape.FarSide()))
            {
                Assert.That(p.x, Is.InRange(-98f, 98f), $"{p} sits outside the slice");
                Assert.That(p.y, Is.InRange(0f, 79f), $"{p} sits outside the slice");
            }
            var deck = shape.Deck();
            var walkway = shape.Walkway();
            float walkLow = walkway.Min(p => p.y), walkHigh = walkway.Max(p => p.y);
            float nearTop = shape.NearSide().Max(p => p.y), farLine = shape.FarSide()[0].y;
            // The walkway sits inside the deck and inside both side lines, so leaning on a rail from outside never counts.
            Assert.Greater(walkLow, nearTop);
            Assert.Less(walkHigh, farLine);
            Assert.GreaterOrEqual(walkLow, deck.Min(p => p.y));
            Assert.LessOrEqual(walkHigh, deck.Max(p => p.y));
            // Room for the player's feet between the sides: their capsule is 6 px tall.
            Assert.Greater(farLine - nearTop - 6f, 12f, "a band for the feet at least 12 px deep");
            // On the deck the player is behind the near rail (their feet stop above where it stands) and in front of the
            // far rail (their head, 6 px over their feet, stops below where it stands).
            Assert.Greater(nearTop, shape.NearFoot()[0].y);
            Assert.Less(farLine - 6f, shape.FarFoot()[0].y);
            // Both ends are open: the sides run end to end, and nothing closes the ends.
            Assert.AreEqual(shape.Left, shape.FarSide()[0].x);
            Assert.AreEqual(shape.Right, shape.FarSide()[1].x);
        }

        static bool Inside(Vector2[] polygon, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                Vector2 a = polygon[i], b = polygon[j];
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside;
        }
    }
}
