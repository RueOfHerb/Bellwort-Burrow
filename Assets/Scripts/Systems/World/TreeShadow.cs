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

        [Header("Shadow (pixels)")]
        [Tooltip("How wide the trunk's shadow is halfway across the map.")]
        [Min(2)] public int widthMiddle = 240;
        [Tooltip("How wide it is at the ends of its run, in the lower right and upper left, when the light is low.")]
        [Min(2)] public int widthEnds = 360;
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
