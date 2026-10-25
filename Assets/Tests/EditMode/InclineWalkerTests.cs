using System.Collections.Generic;
using BellwortBurrow.Systems.World;
using BellwortBurrow.Tiles;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace BellwortBurrow.Tests.EditMode
{
    public class InclineWalkerTests
    {
        /// <summary>A slope tile that rises one way everywhere it's painted.</summary>
        class RisingTile : TileBase, IInclineTile
        {
            public Vector2 uphill = Vector2.up;
            public Vector2 Uphill(Tilemap map, Vector3Int cell) => uphill;
        }

        readonly List<Object> made = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var thing in made)
                if (thing != null) Object.DestroyImmediate(thing);
            made.Clear();
        }

        T Make<T>(T thing) where T : Object { made.Add(thing); return thing; }

        const float Up = 0.85f, Down = 1.10f;

        [Test]
        public void Factor_StraightUpIsUphillSpeed_StraightDownIsDownhillSpeed()
        {
            Assert.AreEqual(Up, InclineWalker.Factor(Vector2.up, Vector2.up, Up, Down), 1e-5f);
            Assert.AreEqual(Down, InclineWalker.Factor(Vector2.down, Vector2.up, Up, Down), 1e-5f);
        }

        [Test]
        public void Factor_AcrossASlopeOrOnLevelGround_IsUnchanged()
        {
            Assert.AreEqual(1f, InclineWalker.Factor(Vector2.right, Vector2.up, Up, Down), 1e-5f);
            Assert.AreEqual(1f, InclineWalker.Factor(Vector2.up, Vector2.zero, Up, Down), 1e-5f);
            Assert.AreEqual(1f, InclineWalker.Factor(Vector2.zero, Vector2.up, Up, Down), 1e-5f, "standing still");
        }

        [Test]
        public void Factor_DiagonalsAndGentlerSlopes_AreInBetween()
        {
            float diagonal = InclineWalker.Factor(new Vector2(1f, 1f), Vector2.up, Up, Down);
            Assert.AreEqual(Mathf.Lerp(1f, Up, Mathf.Sqrt(0.5f)), diagonal, 1e-5f);
            Assert.AreEqual(Mathf.Lerp(1f, Up, 0.5f), InclineWalker.Factor(Vector2.up, Vector2.up * 0.5f, Up, Down), 1e-5f);
            Assert.AreEqual(Mathf.Lerp(1f, Down, 0.5f), InclineWalker.Factor(Vector2.down, Vector2.up * 0.5f, Up, Down), 1e-5f);
            Assert.AreEqual(Up, InclineWalker.Factor(Vector2.up, Vector2.up * 3f, Up, Down), 1e-5f, "never past the steepest");
        }

        [Test]
        public void Velocity_OnARampAlongTheFace_FollowsTheWalkwayAtTheSameSpeed()
        {
            var v = InclineWalker.Velocity(Vector2.right, 4f, Vector2.zero, 0.6f, 0.85f, 1.10f);
            Assert.AreEqual(4f, v.magnitude, 1e-4f, "the same speed");
            Assert.AreEqual(0.6f, v.y / v.x, 1e-4f, "turned up the screen along the walkway");
            var back = InclineWalker.Velocity(Vector2.left, 4f, Vector2.zero, 0.6f, 0.85f, 1.10f);
            Assert.Less(back.y, 0f, "walking back west goes down the screen");
            Assert.AreEqual(new Vector2(0f, 4f), InclineWalker.Velocity(Vector2.up, 4f, Vector2.zero, 0.6f, 0.85f, 1.10f),
                "walking north isn't turned");
            var slow = InclineWalker.Velocity(Vector2.right, 4f, Vector2.right, 0f, 0.85f, 1.10f);
            Assert.AreEqual(4f * 0.85f, slow.x, 1e-4f, "with no lift, just slower uphill");
            Assert.AreEqual(0f, slow.y, 1e-6f);
        }

        [Test]
        public void Factor_IgnoresHowHardTheStickIsPushed()
        {
            Assert.AreEqual(Up, InclineWalker.Factor(Vector2.up * 0.3f, Vector2.up, Up, Down), 1e-5f);
        }

        (InclineWalker walker, Tilemap map, RisingTile tile) Scene()
        {
            var grid = Make(new GameObject("Grid")).AddComponent<Grid>();
            var mapObject = Make(new GameObject("Slopes"));
            mapObject.transform.SetParent(grid.transform);
            var map = mapObject.AddComponent<Tilemap>();
            var tile = Make(ScriptableObject.CreateInstance<RisingTile>());
            map.SetTile(new Vector3Int(2, 3, 0), tile);
            var walker = Make(new GameObject("walker")).AddComponent<InclineWalker>();
            walker.UseMaps(new[] { map });
            return (walker, map, tile);
        }

        [Test]
        public void SpeedFor_OnASlopeTile_ReadsItsUphill()
        {
            var (walker, _, _) = Scene();
            walker.transform.position = new Vector3(2.5f, 3.5f, 0f);
            Assert.AreEqual(Up, walker.SpeedFor(Vector2.up), 1e-5f);
            Assert.AreEqual(Down, walker.SpeedFor(Vector2.down), 1e-5f);
            Assert.AreEqual(1f, walker.SpeedFor(Vector2.left), 1e-5f);
        }

        [Test]
        public void SpeedFor_OffTheSlope_IsUnchanged()
        {
            var (walker, _, _) = Scene();
            walker.transform.position = new Vector3(5.5f, 3.5f, 0f);
            Assert.AreEqual(1f, walker.SpeedFor(Vector2.up), 1e-5f);
        }

        [Test]
        public void SpeedFor_UsesTheFeet()
        {
            var (walker, _, _) = Scene();
            walker.feet = new Vector2(0f, -1f);
            walker.transform.position = new Vector3(2.5f, 4.5f, 0f);
            Assert.AreEqual(Up, walker.SpeedFor(Vector2.up), 1e-5f, "the feet are on the slope a tile below");
        }

        [Test]
        public void SpeedFor_ASteepSlopeGoingEast_SlowsWalkingEast()
        {
            var (walker, _, tile) = Scene();
            tile.uphill = Vector2.right * 0.6f;
            walker.transform.position = new Vector3(2.5f, 3.5f, 0f);
            Assert.AreEqual(Mathf.Lerp(1f, Up, 0.6f), walker.SpeedFor(Vector2.right), 1e-5f);
            Assert.AreEqual(Mathf.Lerp(1f, Down, 0.6f), walker.SpeedFor(Vector2.left), 1e-5f);
        }

        Bridge ArchedBridge(out Collider2D walker)
        {
            var bridge = Make(new GameObject("bridge")).AddComponent<Bridge>();
            bridge.UseGround(new Collider2D[0]);
            bridge.inclineAxis = Vector2.right;
            bridge.incline = new[] { new Vector2(0f, 0f), new Vector2(4f, 1f), new Vector2(8f, 0f) };
            var go = Make(new GameObject("walker"));
            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            walker = go.AddComponent<CircleCollider2D>();
            return bridge;
        }

        [Test]
        public void Bridge_OnTheDeck_UphillFollowsTheArch()
        {
            var bridge = ArchedBridge(out var walker);
            bridge.Step(walker, true);
            Assert.IsTrue(bridge.TryGetUphill(walker, new Vector2(2f, 0f), out var rising));
            Assert.AreEqual(0.25f, rising.x, 1e-4f, "rises a quarter unit per unit toward the crest");
            Assert.AreEqual(0f, rising.y, 1e-4f);
            Assert.IsTrue(bridge.TryGetUphill(walker, new Vector2(6f, 0f), out var falling));
            Assert.AreEqual(-0.25f, falling.x, 1e-4f, "past the crest, uphill is back toward it");
        }

        [Test]
        public void Bridge_OffTheDeck_DoesNotCount()
        {
            var bridge = ArchedBridge(out var walker);
            Assert.IsFalse(bridge.TryGetUphill(walker, new Vector2(2f, 0f), out _));
        }

        [Test]
        public void Bridge_OnTheDeck_OutranksTheSlopeUnderIt()
        {
            var (walker, _, _) = Scene();
            var body = walker.gameObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            var feet = walker.gameObject.AddComponent<CircleCollider2D>();
            var bridge = Make(new GameObject("bridge")).AddComponent<Bridge>();
            bridge.UseGround(new Collider2D[0]);
            walker.transform.position = new Vector3(2.5f, 3.5f, 0f);
            Physics2D.SyncTransforms();
            Assert.AreEqual(Up, walker.SpeedFor(Vector2.up), 1e-5f, "on the slope tile");
            bridge.Step(feet, true);
            Assert.AreEqual(1f, walker.SpeedFor(Vector2.up), 1e-5f, "a level deck over it");
        }
    }
}
