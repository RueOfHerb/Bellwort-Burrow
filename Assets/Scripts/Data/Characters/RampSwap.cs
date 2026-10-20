using System.Collections.Generic;
using UnityEngine;

namespace BellwortBurrow.Data
{
    /// <summary>
    /// Which colors to swap to recolor a player (sys-6). Each Critter Kind names the ramps its fur, markings and eyes are
    /// drawn in; the profile names the ramps to show instead. Each swap is shade for shade (Shadow to Shadow and so on),
    /// so the shading stays. Natural, an empty or unknown choice, or the kind's own ramp swaps nothing.
    /// </summary>
    public static class RampSwap
    {
        /// <summary>Three parts of five shades, with room for one more color.</summary>
        public const int MaxColors = 16;

        public const string Natural = "natural";

        /// <summary>Fills from and to with the colors to swap, and returns how many.</summary>
        public static int Build(CritterKind kind, PlayerProfile profile, RampPalette palette, List<Color32> from, List<Color32> to)
        {
            from.Clear();
            to.Clear();
            if (kind == null || profile == null || palette == null) return 0;

            Add(kind.FurRamp, profile.furColor);
            Add(kind.MarkingsRamp, profile.markingsColor);
            Add(kind.EyeRamp, profile.eyeColor);
            return from.Count;

            void Add(string drawnIn, string chosen)
            {
                var source = palette.Find(drawnIn);
                var target = palette.Find(chosen);
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
}
