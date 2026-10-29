using BellwortBurrow.Systems.World;
using NUnit.Framework;
using UnityEngine;

namespace BellwortBurrow.Tests.EditMode
{
    /// <summary>The ground's heights from its banks, and the log's shadow on them (lvl-5).</summary>
    public class TerrainHeightsTests
    {
        static bool[,] Bank(int w, int h, int fromRow, int rows, int columns)
        {
            var face = new bool[w, h];
            for (int x = 0; x < columns; x++)
                for (int y = fromRow; y < fromRow + rows; y++) face[x, y] = true;
            return face;
        }

        [Test]
        public void NoFacesIsAllLevelGround()
        {
            var levels = TerrainHeights.Solve(new bool[12, 10]);
            foreach (var v in levels) Assert.That(v, Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void ABankAcrossTheMapIsOneLevelUp()
        {
            int w = 20, h = 20;
            var levels = TerrainHeights.Solve(Bank(w, h, 8, 3, w));
            for (int x = 0; x < w; x++)
            {
                Assert.That(levels[2 * w + x], Is.EqualTo(0f).Within(1e-3f));
                Assert.That(levels[15 * w + x], Is.EqualTo(1f).Within(1e-3f));
                Assert.That(float.IsNaN(levels[9 * w + x]), "face cells have no level of their own");
            }
        }

        [Test]
        public void ABankThatEndsInGrassFadesAroundItsEnd()
        {
            int w = 30, h = 20;
            var levels = TerrainHeights.Solve(Bank(w, h, 8, 3, 12));
            float stepAtStart = levels[11 * w] - levels[7 * w];
            float stepFarPast = levels[11 * w + 29] - levels[7 * w + 29];
            Assert.That(stepAtStart, Is.GreaterThan(0.6f), "the bank still steps up where it runs");
            Assert.That(Mathf.Abs(stepFarPast), Is.LessThan(0.25f), "open grass past the bank's end is nearly level");
            foreach (var v in levels) Assert.That(float.IsInfinity(v), Is.False);
        }

        [Test]
        public void AFaceClimbsFromItsFootToItsLip()
        {
            var heights = ScriptableObject.CreateInstance<TerrainHeights>();
            heights.width = 1; heights.height = 5; heights.tilePixels = 16;
            heights.cells = new[] { 0f, float.NaN, float.NaN, float.NaN, 1f };
            heights.faceX = new[] { 0 }; heights.faceBottom = new[] { 1 }; heights.faceTop = new[] { 4 };
            heights.faceLow = new[] { 0f }; heights.faceHigh = new[] { 1f };
            heights.faceEdges = new byte[32];
            for (int i = 0; i < 16; i++) { heights.faceEdges[i * 2] = 4; heights.faceEdges[i * 2 + 1] = 40; }
            var z = heights.PixelLevels();
            Assert.That(z[8 * 16 + 3], Is.EqualTo(0f).Within(1e-4f), "below the face");
            Assert.That(z[(16 + 2) * 16 + 3], Is.EqualTo(0f).Within(1e-4f), "the face's foot");
            Assert.That(z[(16 + 22) * 16 + 3], Is.EqualTo(0.5f).Within(0.03f), "halfway up the face");
            Assert.That(z[(16 + 44) * 16 + 3], Is.EqualTo(1f).Within(1e-4f), "past its lip");
            Assert.That(z[70 * 16 + 3], Is.EqualTo(1f).Within(1e-4f), "on top");
            Object.DestroyImmediate(heights);
        }

        static TerrainHeights FlatWithRamp(RectInt block, TerrainHeights.RampClimb climb, int landing)
        {
            var heights = ScriptableObject.CreateInstance<TerrainHeights>();
            heights.width = 10; heights.height = 10; heights.tilePixels = 16;
            heights.cells = new float[100];
            heights.ramps = new[] { block }; heights.rampClimb = new[] { climb }; heights.rampLanding = new[] { landing };
            heights.rampLow = new[] { 0f }; heights.rampHigh = new[] { 1f };
            return heights;
        }

        [Test]
        public void AnEastRampClimbsALevelAlongItsColumns()
        {
            var heights = FlatWithRamp(new RectInt(2, 2, 6, 7), TerrainHeights.RampClimb.East, 1);
            var z = heights.PixelLevels();
            int pw = 160;
            Assert.That(z[(32 + 20) * pw + 40], Is.EqualTo(0f).Within(1e-4f), "on the landing's walkway");
            Assert.That(z[(32 + 140 - 32) * pw + 40], Is.EqualTo(1f).Within(1e-4f), "above the landing's back wall");
            Assert.That(z[(32 + 60) * pw + 120], Is.EqualTo(0.906f).Within(0.01f), "on the walkway near its top end");
            Assert.That(z[(32 + 20) * pw + 120], Is.InRange(0f, 0.906f), "the walkway's side wall below it");
            Assert.That(z[(32 + 20) * pw + 80], Is.LessThan(z[(32 + 20) * pw + 120] + 1e-4f), "lower toward the landing");
            Object.DestroyImmediate(heights);
        }

        [Test]
        public void ANorthRampClimbsUpTheScreen()
        {
            var heights = FlatWithRamp(new RectInt(2, 2, 4, 7), TerrainHeights.RampClimb.North, 0);
            var z = heights.PixelLevels();
            int pw = 160;
            Assert.That(z[(32 + 1) * pw + 56], Is.LessThan(0.02f), "its foot");
            Assert.That(z[(32 + 56) * pw + 56], Is.EqualTo(0.5f).Within(0.02f), "halfway");
            Assert.That(z[(32 + 111) * pw + 56], Is.GreaterThan(0.98f), "its top");
            Assert.That(z[(32 + 56) * pw + 8], Is.EqualTo(0f).Within(1e-4f), "beside it the ground is its own");
            Object.DestroyImmediate(heights);
        }

        [Test]
        public void TheShadowStepsUpOntoRaisedGround()
        {
            int w = 400, h = 400;
            var start = new Vector2(200, 300);
            var dir = Vector2.down;
            var flat = new float[w * h];
            var raised = new float[w * h];
            for (int i = 0; i < raised.Length; i++) raised[i] = 1f;
            var shift = TreeShadow.ShiftPerLevel(dir, 0f);   // the light straight overhead: only the 3/4 view's lift
            var a = new Color32[w * h];
            var b = new Color32[w * h];
            TreeShadow.PaintOnHeights(a, w, h, start, dir, 100, flat, 0f, shift);
            TreeShadow.PaintOnHeights(b, w, h, start, dir, 100, raised, 0f, shift);
            int x = 200, y = 320;   // above the start: lit on flat ground, in shadow a level up
            Assert.That(a[y * w + x].a, Is.EqualTo(255), "flat ground above the start is lit");
            Assert.That(b[y * w + x].a, Is.EqualTo(0), "a level up, the shadow reaches 48 px higher");
            Assert.That(b[(300 + 50) * w + x].a, Is.EqualTo(255), "and no further");
        }

        [Test]
        public void TheShadowMovesBackTowardTheStumpAsTheLightSinks()
        {
            var dir = new Vector2(-0.45f, -0.89f).normalized;
            var high = TreeShadow.ShiftPerLevel(dir, ShadowCurve.ScaleAt(0.5f, 15f, 70f));
            var low = TreeShadow.ShiftPerLevel(dir, ShadowCurve.ScaleAt(0.1f, 15f, 70f));
            Assert.That(Vector2.Dot(low, dir), Is.GreaterThan(Vector2.Dot(high, dir)), "a longer reach along the shadow when low");
        }

        [Test]
        public void BlocksPaintTheSameAsEveryPixel()
        {
            int w = 320, h = 256;
            var levels = new float[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    levels[y * w + x] = y < 100 ? 0f : y < 140 ? (y - 100) / 40f : 1f + 0.002f * x;
            var start = new Vector2(250, 240);
            var dir = new Vector2(-0.5f, -0.86f).normalized;
            var shift = TreeShadow.ShiftPerLevel(dir, 1.4f);
            var everyPixel = TreeShadow.BlockLevels(levels, w, h);
            for (int i = 0; i < everyPixel.Length; i += 2) { everyPixel[i] = -1000f; everyPixel[i + 1] = 1000f; }
            var a = new Color32[w * h];
            var b = new Color32[w * h];
            TreeShadow.PaintOnHeights(a, w, h, start, dir, 120, levels, 0.5f, shift);
            TreeShadow.PaintOnHeights(b, w, h, start, dir, 120, levels, 0.5f, shift, everyPixel);
            int shaded = 0;
            for (int i = 0; i < a.Length; i++)
            {
                Assert.That(a[i].a, Is.EqualTo(b[i].a), $"pixel {i % w}, {i / w}");
                if (a[i].a == 0) shaded++;
            }
            Assert.That(shaded, Is.GreaterThan(1000));
        }
    }
}
