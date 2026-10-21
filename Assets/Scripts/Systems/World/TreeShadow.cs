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
        [Tooltip("The moon is gone at this hour (5 is 5:00 AM). By then the shadow has crossed to the upper left.")]
        [Range(0f, 24f)] public float moonSets = 5f;
        [Tooltip("The sunrise lights the town from this hour (7 is 7:00 AM). Its shadow starts in the lower right again, at the same pace.")]
        [Range(0f, 24f)] public float sunRises = 7f;
        [Tooltip("Minutes the moonlight takes to fade out before it sets, and the sunlight to fade in after it rises.")]
        [Min(1)] public int fadeMinutes = 20;

        [Header("Light")]
        [Tooltip("The shade while the moon is up: what reaches the town inside the trunk's shadow.")]
        public Color moonShade = new Color32(0xC6, 0xD4, 0xF4, 0xFF);
        [Min(0f)] public float moonShadeIntensity = 0.5f;
        [Tooltip("The moonlight added where the trunk doesn't block it.")]
        public Color moonLight = new Color32(0xC6, 0xD4, 0xF4, 0xFF);
        [Min(0f)] public float moonLightIntensity = 0.4f;
        [Tooltip("The blue hour between moonset and sunrise: no direct light, so no shadow.")]
        public Color blueHour = new Color32(0xB4, 0xC0, 0xEC, 0xFF);
        [Min(0f)] public float blueHourIntensity = 0.62f;
        [Tooltip("The shade while the sun is rising.")]
        public Color sunShade = new Color32(0xC8, 0xB4, 0xD4, 0xFF);
        [Min(0f)] public float sunShadeIntensity = 0.55f;
        [Tooltip("The sunrise light added where the trunk doesn't block it.")]
        public Color sunLight = new Color32(0xFF, 0xC8, 0x8C, 0xFF);
        [Min(0f)] public float sunLightIntensity = 0.5f;

        [Header("Shadow (pixels)")]
        [Tooltip("How wide the trunk's shadow is halfway across the map.")]
        [Min(2)] public int widthMiddle = 80;
        [Tooltip("How wide it is at the ends of its run, in the lower right and upper left, when the light is low.")]
        [Min(2)] public int widthEnds = 128;
    }

    /// <summary>
    /// The light under the tree at a time of day, and the trunk's shadow. The shadow is a band across the map that the
    /// moon pushes from the lower right to the upper left (about 30 degrees) between moonrise and moonset. It's gone in
    /// the blue hour, and comes back in the lower right at sunrise, moving at the same pace, so by the time the player
    /// passes out it's only part of the way across.
    /// - Its edges are clean 2 by 1 pixel stairs, so it reads as pixel art, with a bark wobble and the stub of an old
    ///   branch that ride along with it.
    /// - It's wider at the ends of its run than in the middle.
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
                float up = Mathf.Clamp01((crossing - sinceMoonrise) / (float)fade);
                sky.phase = Phase.Moon;
                sky.ambient = Color.Lerp(s.blueHour, s.moonShade, up);
                sky.ambientIntensity = Mathf.Lerp(s.blueHourIntensity, s.moonShadeIntensity, up);
                sky.direct = s.moonLight;
                sky.directIntensity = s.moonLightIntensity * up;
                sky.travel = sinceMoonrise / (float)crossing;
            }
            else if (sinceSunrise < sunUp)
            {
                float up = Mathf.Clamp01(sinceSunrise / (float)fade);
                sky.phase = Phase.Sunrise;
                sky.ambient = Color.Lerp(s.blueHour, s.sunShade, up);
                sky.ambientIntensity = Mathf.Lerp(s.blueHourIntensity, s.sunShadeIntensity, up);
                sky.direct = s.sunLight;
                sky.directIntensity = s.sunLightIntensity * up;
                sky.travel = sinceSunrise / (float)crossing;
            }

            sky.shadow = sky.directIntensity > 0f && sky.travel <= 1f;
            return sky;
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
            float bulge = 2.6f * Mathf.Sin(2f * Mathf.PI * s / 170f + phase) + 1.1f * Mathf.Sin(2f * Mathf.PI * s / 61f + 2.3f * phase);
            if (stub) bulge += 7f * Mathf.Clamp01((14f - Mathf.Abs(s - 900f)) / 6f);   // where an old branch was
            return Mathf.RoundToInt(bulge);
        }

        static int FloorDiv(int a, int b) => a >= 0 ? a / b : -((-a + b - 1) / b);
    }
}
