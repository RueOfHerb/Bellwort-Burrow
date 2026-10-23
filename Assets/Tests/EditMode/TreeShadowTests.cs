using NUnit.Framework;
using UnityEngine;
using BellwortBurrow.Systems.World;

namespace BellwortBurrow.Tests.EditMode
{
    public class TreeShadowTests
    {
        // The Bellwort Burrow ground: 60 by 34 tiles of 16 pixels.
        const int Width = 960, Height = 544;
        const int SevenPm = 19 * 60, TenPm = 22 * 60, TwoAm = 2 * 60, FiveAm = 5 * 60, SevenAm = 7 * 60, NineAm = 9 * 60;

        readonly TreeShadowSettings settings = new();

        TreeShadow.Sky At(int minute) => TreeShadow.At(minute, settings);

        Color32[] Painted(int minute)
        {
            var sky = At(minute);
            Assert.IsTrue(sky.shadow, $"no shadow at minute {minute}");
            var (lo, hi) = TreeShadow.BandAt(sky.travel, Width, Height, settings.widthMiddle, settings.widthEnds);
            var pixels = new Color32[Width * Height];
            TreeShadow.Paint(pixels, Width, Height, lo, hi);
            return pixels;
        }

        static Vector2 MiddleOfTheShadow(Color32[] pixels, out int count, out int leftmost)
        {
            var sum = Vector2.zero;
            count = 0;
            leftmost = Width;
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    if (pixels[y * Width + x].a == 0)
                    {
                        sum += new Vector2(x, y);
                        count++;
                        leftmost = Mathf.Min(leftmost, x);
                    }
            return count > 0 ? sum / count : new Vector2(-1, -1);
        }

        [Test]
        public void AtSevenPm_TheMoonIsUpAndTheShadowIsInTheLowerRight()
        {
            var sky = At(SevenPm);
            Assert.AreEqual(TreeShadow.Phase.Moon, sky.phase);
            Assert.AreEqual(0f, sky.travel);
            Assert.Greater(sky.directIntensity, 0f);

            var middle = MiddleOfTheShadow(Painted(SevenPm), out int count, out _);
            Assert.Greater(count, 0);
            Assert.Greater(middle.x, Width * 0.75f);
            Assert.Less(middle.y, Height * 0.5f);
        }

        [Test]
        public void ThroughTheNight_TheShadowMovesUpAndToTheLeft()
        {
            var early = MiddleOfTheShadow(Painted(TenPm), out _, out _);
            var late = MiddleOfTheShadow(Painted(TwoAm), out _, out _);
            Assert.Less(late.x, early.x, "It didn't move left.");

            // Up: where it crosses the right side of the map, it climbs.
            Assert.Greater(LowestRowAtTheRightSide(SevenPm + 60), LowestRowAtTheRightSide(SevenPm));
            Assert.Greater(LowestRowAtTheRightSide(SevenPm + 120), LowestRowAtTheRightSide(SevenPm + 60));
        }

        int LowestRowAtTheRightSide(int minute)
        {
            var pixels = Painted(minute);
            for (int y = 0; y < Height; y++)
                if (pixels[y * Width + Width - 1].a == 0) return y;
            return Height;
        }

        [Test]
        public void TheNightStartsDarkerAndBrightens()
        {
            var dusk = At(SevenPm);
            var later = At(TenPm);
            var full = At(1 * 60);
            Assert.Less(dusk.directIntensity, later.directIntensity);
            Assert.Less(later.directIntensity, full.directIntensity);
            Assert.Less(dusk.ambientIntensity, full.ambientIntensity);
            Assert.AreEqual(settings.moonLightIntensity, full.directIntensity, 1e-5f);
            Assert.AreEqual(settings.moonLightIntensity, At(3 * 60).directIntensity, 1e-5f, "It dimmed again before the fade.");
        }

        [Test]
        public void TheBlueHourIsDarkerThanAnyOfTheNight()
        {
            var blue = At(6 * 60);
            int crossing = TreeShadow.CrossingMinutes(settings);
            for (int i = 0; i * 30 < crossing - settings.fadeMinutes; i++)
            {
                int minute = (SevenPm + i * 30) % (24 * 60);
                var night = At(minute);
                Assert.Less(Brightness(blue.ambient, blue.ambientIntensity), Brightness(night.ambient, night.ambientIntensity), $"minute {minute}");
            }
        }

        [Test]
        public void TheMoonIsBlueAndTheSunriseYellow()
        {
            var moon = At(1 * 60).direct;
            Assert.Greater(moon.b, moon.r);
            Assert.Greater(moon.b, moon.g);
            var sun = At(NineAm).direct;
            Assert.Greater(sun.r, sun.b);
            Assert.Greater(sun.g, sun.b);
        }

        static float Brightness(Color c, float intensity) => (c.r + c.g + c.b) / 3f * intensity;

        [Test]
        public void TheMoonFadesBeforeFiveAndTheBlueHourHasNoShadow()
        {
            var fading = At(FiveAm - settings.fadeMinutes / 2);
            Assert.AreEqual(TreeShadow.Phase.Moon, fading.phase);
            Assert.Greater(fading.directIntensity, 0f);
            Assert.Less(fading.directIntensity, settings.moonLightIntensity);

            foreach (int minute in new[] { FiveAm, FiveAm + 30, SevenAm - 1 })
            {
                var sky = At(minute);
                Assert.AreEqual(TreeShadow.Phase.BlueHour, sky.phase, $"minute {minute}");
                Assert.IsFalse(sky.shadow, $"minute {minute}");
                Assert.AreEqual(0f, sky.directIntensity);
            }
        }

        [Test]
        public void AtSunrise_TheShadowComesBackInTheLowerRight()
        {
            var sky = At(SevenAm + settings.fadeMinutes);
            Assert.AreEqual(TreeShadow.Phase.Sunrise, sky.phase);
            Assert.Greater(sky.directIntensity, 0f);
            Assert.AreEqual(settings.sunLightIntensity, At(NineAm).directIntensity, 1e-5f);

            var middle = MiddleOfTheShadow(Painted(SevenAm + 1), out int count, out _);
            Assert.Greater(count, 0);
            Assert.Greater(middle.x, Width * 0.75f);
        }

        [Test]
        public void BothShadowsMoveAtTheSamePace()
        {
            float moonHour = At(SevenPm + 60).travel - At(SevenPm).travel;
            float sunHour = At(SevenAm + 60).travel - At(SevenAm).travel;
            Assert.AreEqual(moonHour, sunHour, 1e-5f);
        }

        [Test]
        public void ByNineAm_TheShadowIsNowhereNearTheOtherSide()
        {
            var sky = At(NineAm);
            Assert.AreEqual(0.2f, sky.travel, 1e-5f);

            // The trunk's shadow is a third of the map wide, so "the other side" is the upper left: by 9:00 AM nothing of
            // it is in the left third, and its middle is still right of the map's middle.
            var middle = MiddleOfTheShadow(Painted(NineAm), out _, out int leftmost);
            Assert.Greater(leftmost, Width / 3, "By 9:00 AM the shadow reached the left third of the map.");
            Assert.Greater(middle.x, Width / 2);
        }

        [Test]
        public void TheShadowIsAsWideAsTheTrunk_AboutAThirdOfTheMap()
        {
            MiddleOfTheShadow(Painted(0), out int count, out _);
            float share = count / (float)(Width * Height);
            Assert.Greater(share, 0.2f);
            Assert.Less(share, 0.45f);
        }

        // The stump's top on the Bellwort Burrow ground, a big map: 140 by 100 tiles.
        const int BigWidth = 2240, BigHeight = 1600;
        static readonly Vector2 StumpTop = new(1680f, 1597f), StumpRim = new(280f, 140f);

        Color32[] FromTheStump(int minute, out Vector2 start, out Vector2 dir, out float length)
        {
            var sky = At(minute);
            Assert.IsTrue(sky.shadow, $"no shadow at minute {minute}");
            int across;
            (dir, length, across) = TreeShadow.FromStump(sky.travel, settings);
            start = TreeShadow.StartOnRim(StumpTop, StumpRim, dir);
            var pixels = new Color32[BigWidth * BigHeight];
            TreeShadow.PaintFromStump(pixels, BigWidth, BigHeight, start, dir, length, across);
            return pixels;
        }

        [Test]
        public void FromTheStump_TheShadowStartsAtTheTopOnTheSouthwestSideAllNight()
        {
            foreach (int minute in new[] { SevenPm + 10, TenPm, 0, TwoAm, FiveAm - 60, SevenAm + 60 })
            {
                var pixels = FromTheStump(minute, out var start, out var dir, out _);
                Assert.Less(dir.x, 0f, $"it doesn't point west at minute {minute}");
                Assert.Less(dir.y, 0f, $"it doesn't point south at minute {minute}");
                Assert.Less(start.x, StumpTop.x, "it starts east of the top's middle");
                Assert.Less(start.y, StumpTop.y, "it starts north of the top's middle");
                int nearStart = 0, behind = 0;
                for (int y = 0; y < BigHeight; y++)
                    for (int x = 0; x < BigWidth; x++)
                    {
                        if (pixels[y * BigWidth + x].a != 0) continue;
                        float along = Vector2.Dot(new Vector2(x, y) - start, dir);
                        if (along < -1f) behind++;
                        if (along < 4f) nearStart++;
                    }
                Assert.AreEqual(0, behind, $"shaded behind the stump's rim at minute {minute}");
                Assert.Greater(nearStart, 0, $"the shadow doesn't start at the rim at minute {minute}");
            }
        }

        [Test]
        public void FromTheStump_ItIsLongestWhenTheLightIsLowAndSwingsThroughTheNight()
        {
            var rise = TreeShadow.FromStump(0.02f, settings);
            var middle = TreeShadow.FromStump(0.5f, settings);
            var set = TreeShadow.FromStump(0.98f, settings);
            Assert.Greater(rise.length, middle.length);
            Assert.Greater(set.length, middle.length);
            // Clockwise of the log at rise (flatter, toward the west), counterclockwise at set (steeper, toward the south).
            float Angle(Vector2 d) => Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            Assert.Less(Angle(rise.dir), Angle(middle.dir));
            Assert.Greater(Angle(set.dir), Angle(middle.dir));
            Assert.AreEqual(settings.logBearing, Angle(middle.dir) + 360f, 0.01f);
        }

        [Test]
        public void FromTheStump_EveryRowIsOneRunAtLeastAsWideAsTheMiddleWidth()
        {
            var pixels = FromTheStump(0, out _, out var dir, out _);
            for (int y = 0; y < BigHeight; y++)
            {
                int runs = 0;
                bool inside = false;
                for (int x = 0; x < BigWidth; x++)
                {
                    bool shaded = pixels[y * BigWidth + x].a == 0;
                    if (shaded && !inside) runs++;
                    inside = shaded;
                }
                Assert.LessOrEqual(runs, 1, $"row {y} breaks into {runs} runs");
            }
            Assert.AreEqual(settings.widthMiddle, TreeShadow.FromStump(0.5f, settings).width);
        }

        [Test]
        public void TheShadowIsWiderAtTheEndsOfItsRun()
        {
            int middle = TreeShadow.WidthAt(0.5f, settings.widthMiddle, settings.widthEnds);
            Assert.AreEqual(settings.widthMiddle, middle);
            Assert.AreEqual(settings.widthEnds, TreeShadow.WidthAt(0f, settings.widthMiddle, settings.widthEnds));
            Assert.AreEqual(settings.widthEnds, TreeShadow.WidthAt(1f, settings.widthMiddle, settings.widthEnds));
            Assert.Greater(TreeShadow.WidthAt(0.2f, settings.widthMiddle, settings.widthEnds), middle);
            for (float t = 0f; t <= 1f; t += 0.05f)
                Assert.AreEqual(0, TreeShadow.WidthAt(t, settings.widthMiddle, settings.widthEnds) % 2, "Widths step by whole pairs of pixels.");
        }

        [Test]
        public void EveryRow_IsOneUnbrokenRunWithCleanEdges()
        {
            var sky = At(0);
            var (lo, hi) = TreeShadow.BandAt(sky.travel, Width, Height, settings.widthMiddle, settings.widthEnds);
            int lastFront = int.MinValue;
            for (int y = 0; y < Height; y++)
            {
                var (from, to) = TreeShadow.RowSpan(y, lo, hi);
                Assert.Less(from, to, $"row {y} has no shadow");
                // The front edge steps a pixel every two rows, with the bark nudging it: nothing sharper than the stub of
                // the old branch.
                if (lastFront != int.MinValue) Assert.LessOrEqual(Mathf.Abs(from - lastFront), 11, $"row {y}");
                lastFront = from;
            }
        }

        [Test]
        public void TheBarkRidesWithTheShadow()
        {
            // A spot on the trunk keeps its bulge as the band moves: compare the front edge at two times, lined up along it.
            var (_, hi1) = TreeShadow.BandAt(0.5f, Width, Height, settings.widthMiddle, settings.widthMiddle);
            int hi2 = hi1 + 10;   // the band moved on: each spot on the trunk is now 2 rows up and 4 pixels left
            int matched = 0;
            for (int y = 0; y < Height - 5; y++)
            {
                int straight1 = -FloorDiv(hi1 - y, 2);
                int straight2 = -FloorDiv(hi2 - (y + 2), 2);
                if (straight1 + 2 * y != straight2 + 2 * (y + 2)) continue;
                var (from1, _) = TreeShadow.RowSpan(y, int.MinValue / 4, hi1);
                var (from2, _) = TreeShadow.RowSpan(y + 2, int.MinValue / 4, hi2);
                Assert.AreEqual(straight1 - from1, straight2 - from2, $"row {y}");
                matched++;
            }
            Assert.Greater(matched, 50);
        }

        static int FloorDiv(int a, int b) => a >= 0 ? a / b : -((-a + b - 1) / b);
    }
}
