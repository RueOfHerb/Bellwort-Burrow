using System.Collections.Generic;
using UnityEngine;

namespace BellwortBurrow.Data
{
    /// <summary>
    /// Which colors to swap to recolor a player (sys-6). Each swap is shade for shade (Shadow to Shadow and so on), so the
    /// shading stays. Natural, an empty or unknown choice, or a ramp swapped for itself swaps nothing.
    /// - The body: each Critter Kind names the ramps its fur and markings are drawn in; the profile names the ones to show.
    /// - The face: the faces are drawn once for every kind, their brows in one coat color and their eyes in one eye ramp
    ///   (the Critter Catalog names both). The brows take the fur color the player shows and the eyes the eye color,
    ///   where Natural means the kind's own.
    /// </summary>
    public static class RampSwap
    {
        /// <summary>How many colors one renderer can swap: the shader's limit. A part is five shades.</summary>
        public const int MaxColors = 16;

        public const string Natural = "natural";

        /// <summary>The body's swaps: fur and markings. Fills from and to, and returns how many.</summary>
        public static int Build(CritterKind kind, PlayerProfile profile, RampPalette palette, List<Color32> from, List<Color32> to)
        {
            from.Clear();
            to.Clear();
            if (kind == null || profile == null || palette == null) return 0;

            Pair(palette, kind.FurRamp, profile.furColor, from, to);
            Pair(palette, kind.MarkingsRamp, profile.markingsColor, from, to);
            return from.Count;
        }

        /// <summary>
        /// The face's swaps: brows drawn in <paramref name="faceFurRamp"/> to the fur color the player shows, eyes drawn in
        /// <paramref name="faceEyeRamp"/> to the eye color. Fills from and to, and returns how many.
        /// </summary>
        public static int BuildFace(CritterKind kind, PlayerProfile profile, RampPalette palette, string faceFurRamp, string faceEyeRamp,
            List<Color32> from, List<Color32> to)
        {
            from.Clear();
            to.Clear();
            if (kind == null || profile == null || palette == null) return 0;

            Pair(palette, faceFurRamp, Showing(palette, profile.furColor, kind.FurRamp), from, to);
            Pair(palette, faceEyeRamp, Showing(palette, profile.eyeColor, kind.EyeRamp), from, to);
            return from.Count;
        }

        /// <summary>The ramp a part shows: the one picked, or the kind's own for Natural, empty or unknown.</summary>
        static string Showing(RampPalette palette, string chosen, string own) => palette.Find(chosen) != null ? chosen : own;

        static void Pair(RampPalette palette, string drawnIn, string shown, List<Color32> from, List<Color32> to)
        {
            var source = palette.Find(drawnIn);
            var target = palette.Find(shown);
            if (source == null || target == null || source == target) return;

            int shades = Mathf.Min(source.colors.Length, target.colors.Length);
            for (int i = 0; i < shades && from.Count < MaxColors; i++)
            {
                from.Add(source.colors[i]);
                to.Add(target.colors[i]);
            }
        }
    }
}
