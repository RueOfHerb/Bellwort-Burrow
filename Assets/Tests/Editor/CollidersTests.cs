using System.Collections.Generic;
using System.Linq;
using BellwortBurrow.EditorTools;
using BellwortBurrow.Tiles;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace BellwortBurrow.Tests.Editor
{
    public class CollidersTests
    {
        [Test]
        public void TheStumpHomesAndRocksAreSolid_ClutterRoofsAndWallPiecesAreNot()
        {
            Assert.IsTrue(Colliders.IsSolid("burrow_stump"));
            Assert.IsTrue(Colliders.IsSolid("burrow_home_red"));
            Assert.IsTrue(Colliders.IsSolid("burrow_home_wide_blue"));
            Assert.IsTrue(Colliders.IsSolid("burrow_nook_untinted"));
            Assert.IsTrue(Colliders.IsSolid("burrow_cottage_brown"));
            Assert.IsTrue(Colliders.IsSolid("burrow_mossy_rock_a"));
            Assert.IsTrue(Colliders.IsSolid("burrow_log_support_a"), "the log's supports stand on their stone ring");
            Assert.IsTrue(Colliders.IsSolid("burrow_log_bench_b"));

            Assert.IsFalse(Colliders.IsSolid("burrow_home_roof_red"), "roofs sit on a home");
            Assert.IsFalse(Colliders.IsSolid("burrow_stump_ladder"), "wall pieces go on the stump");
            Assert.IsFalse(Colliders.IsSolid("burrow_stump_foot_a"), "the stump's foot growth is walked through");
            Assert.IsFalse(Colliders.IsSolid("burrow_fern"));
            Assert.IsFalse(Colliders.IsSolid("burrow_grass_clump_a"));
            Assert.IsFalse(Colliders.IsSolid("burrow_mushrooms"));
            Assert.IsFalse(Colliders.IsSolid("burrow_twig"));
            Assert.IsFalse(Colliders.IsSolid("burrow_bark_chips"));
            Assert.IsFalse(Colliders.IsSolid("burrow_bellworts"));
            Assert.IsFalse(Colliders.IsSolid("brown_base"), "the forest isn't solid yet");
        }

        [Test]
        public void ShadowsStepsStairsAndFootGrowthAreNotSolidLayers()
        {
            Assert.IsTrue(AsepriteSheet.IsSolidLayer("Trunk"));
            Assert.IsTrue(AsepriteSheet.IsSolidLayer("Walls"));
            Assert.IsTrue(AsepriteSheet.IsSolidLayer("Rocks"));
            Assert.IsFalse(AsepriteSheet.IsSolidLayer("Contact shadow"));
            Assert.IsFalse(AsepriteSheet.IsSolidLayer("Glow (unlit)"));
            Assert.IsFalse(AsepriteSheet.IsSolidLayer("Steps"));
            Assert.IsFalse(AsepriteSheet.IsSolidLayer("Stair"));
            Assert.IsFalse(AsepriteSheet.IsSolidLayer("Stair rail"));
            Assert.IsFalse(AsepriteSheet.IsSolidLayer("Foot moss"));
        }

        // A sheet with one block of solid pixels; y counts down from the top.
        static bool[] Sheet(int width, int height, System.Func<int, int, bool> solid)
        {
            var mask = new bool[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    mask[y * width + x] = solid(x, y);
            return mask;
        }

        static SheetSlice Slice(int x, int y, int w, int h, int pivotX, int pivotY) =>
            new SheetSlice { Name = "piece", X = x, Y = y, Width = w, Height = h, HasPivot = true, PivotX = pivotX, PivotY = pivotY };

        [Test]
        public void AFlatFrontedPiece_StandsOnAPatchHalfAsDeepAsItIsWide()
        {
            // A 32 by 20 box whose bottom row is the feet row's top.
            var solid = Sheet(40, 30, (x, y) => x >= 4 && x < 36 && y >= 5 && y < 25);
            var shape = Colliders.Footprint(solid, 40, 30, Slice(0, 0, 40, 30, 20, 25));
            Assert.IsNotNull(shape);
            Assert.AreEqual(-16f, shape.Min(p => p.x), 1e-4f);
            Assert.AreEqual(16f, shape.Max(p => p.x), 1e-4f);
            Assert.AreEqual(0f, shape.Min(p => p.y), 1e-4f, "the front edge is on the feet");
            Assert.AreEqual(16f, shape.Max(p => p.y), 1e-4f, "the patch goes back half the width");
        }

        [Test]
        public void TheFrontEdgeFollowsTheRoots()
        {
            // A trunk with a root reaching 4 px further down on the left than the rest of its foot.
            var solid = Sheet(40, 40, (x, y) => x >= 4 && x < 36 && y >= 2 && (y < 30 || (y < 34 && x < 12)));
            var shape = Colliders.Footprint(solid, 40, 40, Slice(0, 0, 40, 40, 20, 34));
            Vector2 Nearest(float x) => shape.Where(p => p.y < 8).OrderBy(p => Mathf.Abs(p.x - x)).First();
            Assert.AreEqual(0f, Nearest(-12f).y, 1e-4f, "under the root the edge is on the feet");
            Assert.AreEqual(4f, Nearest(10f).y, 1e-4f, "past the root it steps back 4 px");
        }

        [Test]
        public void ARoundFoot_IsMirroredAtTheBack()
        {
            // The front of a round foot: its lowest pixels rise toward the sides, as an ellipse's front half does.
            int W = 64, H = 64, cx = 32, feet = 60;
            var solid = Sheet(W, H, (x, y) =>
            {
                float u = (x + 0.5f - cx) / 30f;
                if (Mathf.Abs(u) > 1f) return false;
                float front = feet - 10f * (1f - Mathf.Sqrt(1f - u * u)) - 10f; // 10 px tall front arc, its middle 10 px up
                return y < front + 10f && y > 0;
            });
            var shape = Colliders.Footprint(solid, W, H, Slice(0, 0, W, H, cx, feet));
            Assert.IsNotNull(shape);
            float depth = shape.Max(p => p.y) - shape.Min(p => p.y);
            Assert.Greater(depth, 15f, "the back reaches as far behind the middle as the front comes before it");
            Assert.Less(depth, 40f);
        }

        [Test]
        public void EavesThatJutOut_AreNotPartOfTheFootprint()
        {
            // A 20 px wide wall under a roof whose eaves stick out 6 px either side, 60 px above the feet.
            var solid = Sheet(40, 100, (x, y) => (x >= 10 && x < 30 && y >= 10 && y < 95) || (x >= 4 && x < 36 && y >= 10 && y < 35));
            var shape = Colliders.Footprint(solid, 40, 100, Slice(0, 0, 40, 100, 20, 95));
            Assert.AreEqual(-10f, shape.Min(p => p.x), 1e-4f);
            Assert.AreEqual(10f, shape.Max(p => p.x), 1e-4f);
            Assert.AreEqual(10f, shape.Max(p => p.y), 1e-4f, "half as deep as the wall is wide");
        }

        [Test]
        public void ASliceWithNothingSolid_HasNoFootprint()
        {
            var solid = new bool[16 * 16];
            Assert.IsNull(Colliders.Footprint(solid, 16, 16, Slice(0, 0, 16, 16, 8, 16)));
        }

        [Test]
        public void TheWaterBlocks_OtherGroundDoesNot()
        {
            Assert.IsTrue(Colliders.IsBlockingGroundSlice("water_set"));
            Assert.IsTrue(Colliders.IsBlockingGroundSlice("water_half_set"));
            Assert.IsTrue(Colliders.IsBlockingGroundSlice("water_slope_set"));
            Assert.IsFalse(Colliders.IsBlockingGroundSlice("path_set"));
            Assert.IsFalse(Colliders.IsBlockingGroundSlice("waterfall_set"), "the waterfall goes over a face, which blocks");
            Assert.IsFalse(Colliders.IsBlockingGroundSlice("grass_00"));
            Assert.AreEqual(Tile.ColliderType.Sprite, GroundAutoTiles.ColliderFor(true));
            Assert.AreEqual(Tile.ColliderType.None, GroundAutoTiles.ColliderFor(false));
        }

        [Test]
        public void ABankFace_CollidesAsWholeTiles_UnlessMadeOpen()
        {
            var grid = new GameObject("Grid", typeof(Grid));
            var layer = new GameObject("Banks", typeof(Tilemap));
            layer.transform.SetParent(grid.transform);
            var map = layer.GetComponent<Tilemap>();
            var face = ScriptableObject.CreateInstance<FaceTile>();
            try
            {
                map.SetTile(Vector3Int.zero, face);
                Assert.AreEqual(Tile.ColliderType.Grid, map.GetColliderType(Vector3Int.zero));
                face.solid = false;
                map.RefreshTile(Vector3Int.zero);
                Assert.AreEqual(Tile.ColliderType.None, map.GetColliderType(Vector3Int.zero));
            }
            finally
            {
                Object.DestroyImmediate(face);
                Object.DestroyImmediate(grid);
            }
        }
    }
}
