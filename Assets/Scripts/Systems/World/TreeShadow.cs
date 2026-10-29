using System;
using UnityEngine;

namespace BellwortBurrow.Systems.World
{
    /// <summary>
    /// How the light under the Bellwort Tree runs through the player's day, and when, where and how wide the trunk's
    /// shadow falls. Set on the <see cref="TreeShadowClock"/> in a zone under the tree.
    /// </summary>
    [Serializable]
    public class TreeShadowSettings
    {
        [Header("When (hours, 0 to 24)")]
        [Tooltip("The moon lights the town from this hour (19 is 7:00 PM). Its shadow starts in the lower right.")]
        [Range(0f, 24f)] public float moonRises = 19f;
        [Tooltip("The moonlight is at its brightest from this hour (1 is 1:00 AM). The night starts darker and brightens until then.")]
        [Range(0f, 24f)] public float moonFull = 1f;
        [Tooltip("The moon is gone at this hour (5 is 5:00 AM). By then the shadow has crossed to the upper left.")]
        [Range(0f, 24f)] public float moonSets = 5f;
        [Tooltip("The sunrise lights the town from this hour (7 is 7:00 AM). Its shadow starts in the lower right again, at the same pace.")]
        [Range(0f, 24f)] public float sunRises = 7f;
        [Tooltip("The sunlight is at its brightest from this hour (9 is 9:00 AM).")]
        [Range(0f, 24f)] public float sunFull = 9f;
        [Tooltip("Minutes the moonlight takes to fade into the blue hour before it sets, and the sunlight to fade in after it rises.")]
        [Min(1)] public int fadeMinutes = 40;

        [Header("Moon: a blue light")]
        [Tooltip("The shade while the moon is up: what reaches the town inside the trunk's shadow, at the moon's brightest.")]
        public Color moonShade = new Color32(0x8E, 0xA4, 0xE0, 0xFF);
        [Min(0f)] public float moonShadeIntensity = 0.55f;
        [Tooltip("The moonlight added where the trunk doesn't block it, at the moon's brightest.")]
        public Color moonLight = new Color32(0xA8, 0xC0, 0xF8, 0xFF);
        [Min(0f)] public float moonLightIntensity = 0.45f;
        [Tooltip("How bright the moon is when it rises, as a share of its brightest (0 to 1).")]
        [Range(0f, 1f)] public float moonStart = 0.6f;

        [Header("Blue hour: no direct light, so no shadow")]
        [Tooltip("Between moonset and sunrise, the darkest part of the night.")]
        public Color blueHour = new Color32(0x6A, 0x7C, 0xC4, 0xFF);
        [Min(0f)] public float blueHourIntensity = 0.3f;

        [Header("Sunrise: a yellow light")]
        [Tooltip("The shade while the sun is up, at its brightest.")]
        public Color sunShade = new Color32(0xB0, 0xA8, 0x90, 0xFF);
        [Min(0f)] public float sunShadeIntensity = 0.55f;
        [Tooltip("The sunlight added where the trunk doesn't block it, at its brightest.")]
        public Color sunLight = new Color32(0xFF, 0xE6, 0xA0, 0xFF);
        [Min(0f)] public float sunLightIntensity = 0.5f;
        [Tooltip("How bright the sun is once it has faded in, as a share of its brightest (0 to 1).")]
        [Range(0f, 1f)] public float sunStart = 0.6f;

        [Header("Shadow curve (see ShadowCurve)")]
        [Tooltip("The light's elevation at rise and set, in degrees. Above zero, or the shadow would run off forever.")]
        [Range(1f, 45f)] public float elevationRise = 15f;
        [Tooltip("The light's elevation at the middle of its run, in degrees.")]
        [Range(10f, 90f)] public float elevationPeak = 70f;
        [Tooltip("How far the shadow is pivoted clockwise at rise (and counterclockwise at set), in degrees.")]
        [Range(0f, 90f)] public float pivotDegrees = 20f;

        [Header("Shadow (pixels)")]
        [Tooltip("How wide the trunk's shadow is halfway across the map.")]
        [Min(2)] public int widthMiddle = 240;
        [Tooltip("How wide it is at the ends of its run, in the lower right and upper left, when the light is low.")]
        [Min(2)] public int widthEnds = 360;

        [Header("From the stump (when the clock has a stump)")]
        [Tooltip("Which way the fallen log runs off the stump at the middle of the night, in degrees (0 is east, 90 " +
                 "north, 180 west, 270 south). The log is overhead, past the map's lower left edge, so its shadow always " +
                 "runs off the map; it swings round this by Pivot Degrees through the night.")]
        [Range(0f, 360f)] public float logBearing = 243.4f;
    }

    /// <summary>
    /// The light under the tree at a time of day, and the trunk's shadow. The shadow is a band across the map that the
    /// moon pushes from the lower right to the upper left (about 30 degrees) between moonrise and moonset. It's gone in
    /// the blue hour, and comes back in the lower right at sunrise, moving at the same pace, so by the time the player
    /// passes out it's only part of the way across.
    /// - Its edges are clean 2 by 1 pixel stairs, so it reads as pixel art, with a bark wobble and the stub of an old
    ///   branch that ride along with it.
    /// - It's wider at the ends of its run than in the middle, and as wide as the trunk is, about a third of the map.
    /// - The moon gives a blue light that starts dim and brightens into the night; the blue hour is the darkest part;
    ///   the sunrise gives a yellow light.
    /// </summary>
    public static class TreeShadow
    {
        public enum Phase { Moon, BlueHour, Sunrise }

        /// <summary>The light at one minute: the shade everywhere (ambient), the light the trunk blocks (direct), and the shadow.</summary>
        public struct Sky
        {
            public Phase phase;
            public Color ambient;
            public float ambientIntensity;
            public Color direct;
            public float directIntensity;

            /// <summary>How far the shadow is across the map: 0 in the lower right, 1 in the upper left.</summary>
            public float travel;

            /// <summary>False when there's no direct light to cast one, or it has run off the map.</summary>
            public bool shadow;
        }

        const int MinutesPerDay = 24 * 60;

        static int Minute(float hour) => Mathf.RoundToInt(hour * 60f) % MinutesPerDay;
        static int Wrap(int minutes) => ((minutes % MinutesPerDay) + MinutesPerDay) % MinutesPerDay;

        /// <summary>Minutes the shadow takes to cross the map: the moon's hours. The sunrise shadow keeps the same pace.</summary>
        public static int CrossingMinutes(TreeShadowSettings s)
        {
            int minutes = Wrap(Minute(s.moonSets) - Minute(s.moonRises));
            return minutes == 0 ? MinutesPerDay : minutes;
        }

        public static Sky At(int minuteOfDay, TreeShadowSettings s)
        {
            int crossing = CrossingMinutes(s);
            int sinceMoonrise = Wrap(minuteOfDay - Minute(s.moonRises));
            int sinceSunrise = Wrap(minuteOfDay - Minute(s.sunRises));
            int sunUp = Wrap(Minute(s.moonRises) - Minute(s.sunRises));
            int fade = Math.Max(1, s.fadeMinutes);

            var sky = new Sky
            {
                phase = Phase.BlueHour,
                ambient = s.blueHour,
                ambientIntensity = s.blueHourIntensity,
                direct = s.moonLight,
                directIntensity = 0f,
                travel = 0f,
                shadow = false
            };

            if (sinceMoonrise < crossing)
            {
                // Dimmer at moonrise, brightening until the moon's full hour, then fading into the blue hour.
                float strength = Brightening(sinceMoonrise, Wrap(Minute(s.moonFull) - Minute(s.moonRises)), s.moonStart);
                float up = Mathf.Clamp01((crossing - sinceMoonrise) / (float)fade);
                sky.phase = Phase.Moon;
                sky.ambient = Color.Lerp(s.blueHour, s.moonShade, up);
                sky.ambientIntensity = Mathf.Lerp(s.blueHourIntensity, s.moonShadeIntensity * strength, up);
                sky.direct = s.moonLight;
                sky.directIntensity = s.moonLightIntensity * strength * up;
                sky.travel = sinceMoonrise / (float)crossing;
            }
            else if (sinceSunrise < sunUp)
            {
                // Fading in out of the blue hour, then brightening until the sun's full hour.
                float strength = Brightening(sinceSunrise, Wrap(Minute(s.sunFull) - Minute(s.sunRises)), s.sunStart);
                float up = Mathf.Clamp01(sinceSunrise / (float)fade);
                sky.phase = Phase.Sunrise;
                sky.ambient = Color.Lerp(s.blueHour, s.sunShade, up);
                sky.ambientIntensity = Mathf.Lerp(s.blueHourIntensity, s.sunShadeIntensity * strength, up);
                sky.direct = s.sunLight;
                sky.directIntensity = s.sunLightIntensity * strength * up;
                sky.travel = sinceSunrise / (float)crossing;
            }

            sky.shadow = sky.directIntensity > 0f && sky.travel <= 1f;
            return sky;
        }

        // A light's strength: start (0 to 1) when it comes up, easing to 1 after minutesToFull.
        static float Brightening(int minutesUp, int minutesToFull, float start)
        {
            if (minutesToFull <= 0) return 1f;
            float t = Mathf.Clamp01(minutesUp / (float)minutesToFull);
            return Mathf.Lerp(start, 1f, t * t * (3f - 2f * t));
        }

        /// <summary>The shadow's width across, in pixels, at a point in its run: widest at the ends, always even.</summary>
        public static int WidthAt(float travel, int widthMiddle, int widthEnds)
        {
            float edge = 2f * Mathf.Clamp01(travel) - 1f;
            float width = widthMiddle + (widthEnds - widthMiddle) * edge * edge;
            return Math.Max(2, Mathf.RoundToInt(width / 2f) * 2);
        }

        // Positions across the band are measured as c = y - 2x (pixels, y up). Lines of one c are the band's 2 by 1
        // stairs; c grows toward the upper left, sqrt(5) to a pixel of distance.
        static readonly float Root5 = Mathf.Sqrt(5f);

        /// <summary>
        /// The band for a map of width by height pixels: everything from c = lo to c = hi is in shadow (before the
        /// wobble). At travel 0 its back edge touches the lower right corner; at 1 its front edge touches the upper left.
        /// </summary>
        public static (int lo, int hi) BandAt(float travel, int width, int height, int widthMiddle, int widthEnds)
        {
            int Across(float t) => Mathf.RoundToInt(WidthAt(t, widthMiddle, widthEnds) * Root5);
            int lowest = -2 * (width - 1), highest = height - 1;
            float from = lowest + Across(0f) / 2, to = highest - Across(1f) / 2;
            int middle = Mathf.RoundToInt(Mathf.Lerp(from, to, Mathf.Clamp01(travel)));
            int across = Across(travel);
            int lo = middle - across / 2;
            return (lo, lo + across);
        }

        /// <summary>
        /// Paints the band into a width by height cookie (row 0 at the bottom): clear in the shadow, white where the
        /// light gets through. Each row of the band is one run from its front edge to its back edge.
        /// </summary>
        public static void Paint(Color32[] pixels, int width, int height, int lo, int hi)
        {
            var light = new Color32(255, 255, 255, 255);
            var shade = new Color32(0, 0, 0, 0);
            for (int y = 0; y < height; y++)
            {
                var (from, to) = RowSpan(y, lo, hi);
                int row = y * width;
                for (int x = 0; x < width; x++)
                    pixels[row + x] = x >= from && x <= to ? shade : light;
            }
        }

        /// <summary>Long enough to run off any map: the fallen log reaches past the map's lower left edge.</summary>
        public const float PastTheMap = 1e6f;

        /// <summary>
        /// The fallen log's shadow, from where the log rests on the stump: the way it points (a unit vector, y up), how
        /// long it is and how wide, at a point in the light's run (0 at rise, 1 at set). The log lies overhead and runs
        /// on past the map's lower left edge, so the shadow always runs off the map. It points along the log at the middle
        /// of the run and swings clockwise of that at rise and counterclockwise at set (<see cref="ShadowCurve.AngleAt"/>).
        /// </summary>
        public static (Vector2 dir, float length, int width) FromStump(float travel, TreeShadowSettings s)
        {
            float u = Mathf.Clamp01(travel);
            float bearing = (s.logBearing - ShadowCurve.AngleAt(u, s.pivotDegrees)) * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Cos(bearing), Mathf.Sin(bearing));
            return (dir, PastTheMap, WidthAt(u, s.widthMiddle, s.widthEnds));
        }

        /// <summary>
        /// Where the shadow starts on the stump's top, for a top whose middle is at centre (cookie pixels) and whose rim
        /// is an ellipse with radii (across, deep): the point on the rim the shadow points out of.
        /// </summary>
        public static Vector2 StartOnRim(Vector2 centre, Vector2 radii, Vector2 dir)
        {
            float r = 1f / Mathf.Sqrt(dir.x * dir.x / (radii.x * radii.x) + dir.y * dir.y / (radii.y * radii.y));
            return centre + dir * r;
        }

        /// <summary>
        /// Paints the fallen log's shadow from the stump into a width by height cookie (row 0 at the bottom): a strip from
        /// start, pointing dir, length long and across wide (centred on the line from start), clear in the shadow and
        /// white where the light gets through. Nothing behind start is shaded. Each row is one run with whole-pixel
        /// edges; its long sides carry the same bark wobble and old branch stub as the band's.
        /// </summary>
        public static void PaintFromStump(Color32[] pixels, int width, int height, Vector2 start, Vector2 dir, float length, int across)
        {
            var light = new Color32(255, 255, 255, 255);
            var shade = new Color32(0, 0, 0, 0);
            dir = dir.normalized;
            var side = new Vector2(-dir.y, dir.x);
            float half = across / 2f;
            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                float dy = y - start.y;
                float lo = float.NegativeInfinity, hi = float.PositiveInfinity;
                // along the shadow: 0 to length; across it: within half (and the bark's bulge) of its middle line
                Clip(dir.x, dy * dir.y, 0f, length, ref lo, ref hi);
                float mid = float.IsInfinity(lo) || float.IsInfinity(hi) ? 0f : (lo + hi) / 2f;
                int along = Mathf.RoundToInt(mid * dir.x + dy * dir.y);
                float left = half + Bark(along, 2.1f, true), right = half + Bark(along, 0.4f, false);
                Clip(side.x, dy * side.y, -right, left, ref lo, ref hi);
                int from = lo > hi ? int.MaxValue : Mathf.CeilToInt(start.x + lo);
                int to = lo > hi ? int.MinValue : Mathf.FloorToInt(start.x + hi);
                for (int x = 0; x < width; x++)
                    pixels[row + x] = x >= from && x <= to ? shade : light;
            }
        }

        /// <summary>
        /// Like PaintFromStump, on ground that rises and falls (lvl-5): each pixel at a level other than the stump's
        /// takes the shadow of the flat ground shiftPerLevel away per level, so the shadow steps back toward the stump on
        /// higher ground, on past it on lower ground, and runs down bank and cliff faces. levels holds each pixel's
        /// level (TerrainHeights.PixelLevels), baseLevel the stump's. blockRowFrom and blockRowTo paint only those rows
        /// of blocks, so the clock can spread a repaint over a few frames.
        /// </summary>
        public static void PaintOnHeights(Color32[] pixels, int width, int height, Vector2 start, Vector2 dir, int across,
                                          float[] levels, float baseLevel, Vector2 shiftPerLevel, float[] blockLevels = null,
                                          int blockRowFrom = 0, int blockRowTo = int.MaxValue)
        {
            var light = new Color32(255, 255, 255, 255);
            var shade = new Color32(0, 0, 0, 0);
            dir = dir.normalized;
            var side = new Vector2(-dir.y, dir.x);
            float half = across / 2f;
            const float barkMost = 17f;   // the bark's bulge and the branch stub reach no further, plus rounding
            blockLevels ??= BlockLevels(levels, width, height);
            int blocksX = (width + Block - 1) / Block, blocksY = (height + Block - 1) / Block;
            Span<Vector2> corners = stackalloc Vector2[8];
            for (int by = Math.Max(0, blockRowFrom); by < Math.Min(blocksY, blockRowTo); by++)
            for (int bx = 0; bx < blocksX; bx++)
            {
                int x0 = bx * Block, y0 = by * Block;
                int x1 = Math.Min(width, x0 + Block), y1 = Math.Min(height, y0 + Block);
                int k = (by * blocksX + bx) * 2;
                float lo = blockLevels[k] - baseLevel, hi = blockLevels[k + 1] - baseLevel;
                // Where this block's pixels look in the flat shadow: its corners, moved by the lowest and highest level.
                int c = 0;
                for (int e = 0; e < 2; e++)
                {
                    float dz = e == 0 ? lo : hi;
                    var o = dz * shiftPerLevel - start;
                    corners[c++] = new Vector2(x0 - 1, y0 - 1) + o;
                    corners[c++] = new Vector2(x1 + 1, y0 - 1) + o;
                    corners[c++] = new Vector2(x0 - 1, y1 + 1) + o;
                    corners[c++] = new Vector2(x1 + 1, y1 + 1) + o;
                }
                bool allBehind = true, allLeft = true, allRight = true, allIn = true;
                foreach (var p in corners)
                {
                    float pa = p.x * dir.x + p.y * dir.y, pb = p.x * side.x + p.y * side.y;
                    allBehind &= pa < 0f;
                    allLeft &= pb > half + barkMost;
                    allRight &= pb < -(half + barkMost);
                    allIn &= pa >= 1f && Mathf.Abs(pb) <= half - barkMost;
                }
                if (allBehind || allLeft || allRight || allIn)
                {
                    var fill = allIn ? shade : light;
                    for (int y = y0; y < y1; y++)
                        for (int x = x0; x < x1; x++) pixels[y * width + x] = fill;
                    continue;
                }
                for (int y = y0; y < y1; y++)
                {
                    int row = y * width;
                    for (int x = x0; x < x1; x++)
                    {
                        float dz = levels[row + x] - baseLevel;
                        float lx = Mathf.Round(x + dz * shiftPerLevel.x) - start.x;
                        float ly = Mathf.Round(y + dz * shiftPerLevel.y) - start.y;
                        float pa = lx * dir.x + ly * dir.y, pb = lx * side.x + ly * side.y;
                        bool shaded = false;
                        if (pa >= 0f && Mathf.Abs(pb) <= half + barkMost)
                        {
                            int along = Mathf.RoundToInt(pa);
                            shaded = pb <= half + Bark(along, 2.1f, true) && pb >= -(half + Bark(along, 0.4f, false));
                        }
                        pixels[row + x] = shaded ? shade : light;
                    }
                }
            }
        }

        /// <summary>The side of the blocks PaintOnHeights settles at once where no edge can fall.</summary>
        public const int Block = 16;

        /// <summary>The lowest and highest level in each Block by Block square of pixels, for PaintOnHeights.</summary>
        public static float[] BlockLevels(float[] levels, int width, int height)
        {
            int blocksX = (width + Block - 1) / Block, blocksY = (height + Block - 1) / Block;
            var ranges = new float[blocksX * blocksY * 2];
            for (int by = 0; by < blocksY; by++)
            for (int bx = 0; bx < blocksX; bx++)
            {
                float lo = float.PositiveInfinity, hi = float.NegativeInfinity;
                for (int y = by * Block; y < Math.Min(height, (by + 1) * Block); y++)
                    for (int x = bx * Block; x < Math.Min(width, (bx + 1) * Block); x++)
                    {
                        float v = levels[y * width + x];
                        if (v < lo) lo = v;
                        if (v > hi) hi = v;
                    }
                ranges[(by * blocksX + bx) * 2] = lo;
                ranges[(by * blocksX + bx) * 2 + 1] = hi;
            }
            return ranges;
        }

        /// <summary>How far, per level, the shadow on raised ground is looked up in the flat one: along the shadow by
        /// the light's stretch (scale(u), lvl-2.11), and down the screen because raised ground is drawn a level higher.</summary>
        public static Vector2 ShiftPerLevel(Vector2 dir, float scale) =>
            TerrainHeights.LevelPixels * (scale * dir.normalized - Vector2.up);

        // Narrows [lo, hi] (x measured from the start) to where a * x + b lies between min and max.
        static void Clip(float a, float b, float min, float max, ref float lo, ref float hi)
        {
            if (Mathf.Abs(a) < 1e-6f)
            {
                if (b < min || b > max) { lo = 1f; hi = 0f; }
                return;
            }
            float p = (min - b) / a, q = (max - b) / a;
            lo = Mathf.Max(lo, Mathf.Min(p, q));
            hi = Mathf.Min(hi, Mathf.Max(p, q));
        }

        /// <summary>The first and last pixel of the band in row y (from greater than to when the row misses it).</summary>
        public static (int from, int to) RowSpan(int y, int lo, int hi)
        {
            // The back edge (lower right side): c >= lo, so x <= (y - lo) / 2.
            int back = FloorDiv(y - lo, 2);
            back += Bark(back + 2 * y, 0.4f, false);
            // The front edge (upper left side): c <= hi, so x >= (y - hi) / 2.
            int front = -FloorDiv(hi - y, 2);
            front -= Bark(front + 2 * y, 2.1f, true);
            return (front, back);
        }

        /// <summary>
        /// How far an edge bulges out, in whole pixels, at a spot along the band (x + 2y, which stays put on the trunk as
        /// the band moves). Changes only every few pixels, so the edge kinks instead of fraying.
        /// </summary>
        static int Bark(int along, float phase, bool stub)
        {
            float s = FloorDiv(along, 8) * 8;
            float bulge = 4f * Mathf.Sin(2f * Mathf.PI * s / 260f + phase) + 1.5f * Mathf.Sin(2f * Mathf.PI * s / 83f + 2.3f * phase);
            if (stub) bulge += 10f * Mathf.Clamp01((20f - Mathf.Abs(s - 900f)) / 8f);   // where an old branch was
            return Mathf.RoundToInt(bulge);
        }

        static int FloorDiv(int a, int b) => a >= 0 ? a / b : -((-a + b - 1) / b);
    }
}
