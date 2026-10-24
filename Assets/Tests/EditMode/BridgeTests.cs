using System.Collections.Generic;
using System.Linq;
using BellwortBurrow.Systems.World;
using NUnit.Framework;
using UnityEngine;

namespace BellwortBurrow.Tests.EditMode
{
    public class BridgeTests
    {
        readonly List<Object> made = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var thing in made)
                if (thing != null) Object.DestroyImmediate(thing);
            made.Clear();
        }

        T Make<T>(T thing) where T : Object { made.Add(thing); return thing; }

        Collider2D Walker(RigidbodyType2D type = RigidbodyType2D.Dynamic, bool trigger = false)
        {
            var go = Make(new GameObject("walker"));
            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = type;
            body.gravityScale = 0f;
            var collider = go.AddComponent<CircleCollider2D>();
            collider.isTrigger = trigger;
            return collider;
        }

        Collider2D Water()
        {
            var go = Make(new GameObject("Water"));
            return go.AddComponent<BoxCollider2D>();
        }

        Bridge MakeBridge(params Collider2D[] ground)
        {
            var bridge = Make(new GameObject("bridge")).AddComponent<Bridge>();
            bridge.UseGround(ground);
            return bridge;
        }

        [Test]
        public void HeightAt_RunsStraightBetweenPoints_AndLevelPastTheEnds()
        {
            var line = new[] { new Vector2(-2f, 1f), new Vector2(0f, 3f), new Vector2(2f, 3f) };
            Assert.AreEqual(2f, Bridge.HeightAt(line, -1f), 1e-5f);
            Assert.AreEqual(3f, Bridge.HeightAt(line, 1f), 1e-5f);
            Assert.AreEqual(1f, Bridge.HeightAt(line, -5f), 1e-5f);
            Assert.AreEqual(3f, Bridge.HeightAt(line, 5f), 1e-5f);
            var backwards = line.Reverse().ToArray();
            Assert.AreEqual(2f, Bridge.HeightAt(backwards, -1f), 1e-5f, "a line given right to left reads the same");
            Assert.AreEqual(1f, Bridge.HeightAt(backwards, -5f), 1e-5f);
        }

        [Test]
        public void OnTheDeck_TheWalkerCrossesTheWater_AndStopsWhenItStepsOff()
        {
            var water = Water();
            var walker = Walker();
            var bridge = MakeBridge(water);

            bridge.Step(walker, true);
            Assert.IsTrue(Physics2D.GetIgnoreCollision(walker, water));
            Assert.IsTrue(bridge.IsOnDeck(walker));

            bridge.Step(walker, false);
            Assert.IsFalse(Physics2D.GetIgnoreCollision(walker, water));
            Assert.IsFalse(bridge.IsOnDeck(walker));
        }

        [Test]
        public void SteppingOffOneDeckOntoAnother_KeepsTheWalkerAcross()
        {
            var water = Water();
            var walker = Walker();
            var first = MakeBridge(water);
            var second = MakeBridge(water);

            first.Step(walker, true);
            second.Step(walker, true);
            first.Step(walker, false);
            Assert.IsTrue(Physics2D.GetIgnoreCollision(walker, water), "still on the second deck");
            second.Step(walker, false);
            Assert.IsFalse(Physics2D.GetIgnoreCollision(walker, water));
        }

        [Test]
        public void OnlyThingsThatWalkCross()
        {
            var water = Water();
            var bridge = MakeBridge(water);
            var trigger = Walker(trigger: true);
            var still = Walker(RigidbodyType2D.Kinematic);

            bridge.Step(trigger, true);
            bridge.Step(still, true);
            Assert.IsFalse(Physics2D.GetIgnoreCollision(trigger, water));
            Assert.IsFalse(Physics2D.GetIgnoreCollision(still, water));
            Assert.IsFalse(bridge.IsOnDeck(trigger));
        }

        [Test]
        public void TurningTheBridgeOff_PutsTheWaterBackForWhoeverIsOnIt()
        {
            var water = Water();
            var walker = Walker();
            var bridge = MakeBridge(water);
            bridge.Step(walker, true);
            bridge.enabled = false;
            Assert.IsFalse(Physics2D.GetIgnoreCollision(walker, water));
        }

        [Test]
        public void OnARisingDeck_TheWalkerIsCarriedUpAndDownWithIt()
        {
            var walker = Walker();
            var body = walker.attachedRigidbody;
            var bridge = MakeBridge();
            bridge.inclineAxis = Vector2.right;
            bridge.incline = new[] { new Vector2(0f, 0f), new Vector2(2f, 1f), new Vector2(4f, 0f) };

            body.position = Vector2.zero;
            bridge.Step(walker, true);
            body.position = new Vector2(1f, 0f);
            bridge.Carry();
            Assert.AreEqual(0.5f, body.position.y, 1e-4f, "half way up the rise");
            bridge.Carry();
            Assert.AreEqual(0.5f, body.position.y, 1e-4f, "standing still, it stays put");
            body.position = new Vector2(2f, body.position.y + 0.25f);
            bridge.Carry();
            Assert.AreEqual(1.25f, body.position.y, 1e-4f, "at the top, a little further across");
            body.position = new Vector2(4f, body.position.y);
            bridge.Carry();
            Assert.AreEqual(0.25f, body.position.y, 1e-4f, "down again at the far end");

            bridge.Step(walker, false);
            body.position = new Vector2(2f, body.position.y);
            bridge.Carry();
            Assert.AreEqual(0.25f, body.position.y, 1e-4f, "off the deck, nothing carries it");
        }

        [Test]
        public void OnADiagonalDeck_TheWalkerIsCarriedByHowFarAlongItIs()
        {
            var walker = Walker();
            var body = walker.attachedRigidbody;
            var bridge = MakeBridge();
            bridge.inclineAxis = new Vector2(1f, -1f);
            bridge.incline = new[] { new Vector2(0f, 0f), new Vector2(Mathf.Sqrt(2f), 1f) };

            body.position = Vector2.zero;
            bridge.Step(walker, true);
            // One step southeast on the ground, (1, -1), climbs a unit: it shows at (1, 0).
            body.position = new Vector2(1f, -1f);
            bridge.Carry();
            Assert.AreEqual(0f, body.position.y, 1e-4f);
            bridge.Carry();
            Assert.AreEqual(0f, body.position.y, 1e-4f, "no creeping while it stands there");
        }

        [Test]
        public void RailStrips_SortByWhereTheRailStandsUnderThem()
        {
            // A 16 by 16 rail with its feet at the bottom middle, standing on a line from (-0.5, 0.5) to (0.5, 0.25).
            var texture = Make(new Texture2D(16, 16));
            var sprite = Make(Sprite.Create(texture, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0f), 16f));
            var bridge = MakeBridge();
            bridge.stripWidth = 4;
            bridge.nearRail = new Bridge.Rail
            {
                sprite = sprite,
                foot = new[] { new Vector2(-0.5f, 0.5f), new Vector2(0.5f, 0.25f) },
                strips = new[] { 0, 4, 8, 12 },
            };
            bridge.BuildRails();
            bridge.BuildRails();
            Assert.AreEqual(4, bridge.RailStrips.Count, "building again replaces the strips");

            foreach (var strip in bridge.RailStrips)
            {
                var position = strip.transform.localPosition;
                Assert.AreEqual(Bridge.HeightAt(bridge.nearRail.foot, position.x), position.y, 1e-4f,
                    $"{strip.name} stands where the rail does");
                var picture = strip.GetComponent<SpriteRenderer>();
                Assert.AreEqual(SpriteSortPoint.Pivot, picture.spriteSortPoint);
                Assert.AreEqual(4f, picture.sprite.rect.width);
                // Its pixels land where they were on the whole rail: the strip's left edge in units from the feet.
                float left = position.x - picture.sprite.pivot.x / 16f;
                Assert.AreEqual(int.Parse(strip.name.Split(' ').Last()) / 16f - 0.5f, left, 1e-4f);
            }
            Assert.AreEqual(-0.375f, bridge.RailStrips[0].transform.localPosition.x, 1e-4f);
        }
    }
}
