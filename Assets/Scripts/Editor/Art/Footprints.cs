using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using BellwortBurrow.Systems.World;
using UnityEngine;

namespace BellwortBurrow.EditorTools
{
    /// <summary>
    /// How many base tiles each placeable piece is rooted on, where it meets the map (see <see cref="GridFootprint"/>).
    /// - Young trees and stumps: 1 tile. Tall and giant trees (and their pieces): 2 tiles.
    /// - Everything else: the tiles under its front, from the art itself: how wide it is in the tile above its feet,
    ///   in whole tiles (at least 1). So a cottage roots on 5 or 6 tiles and a boulder on 2.
    /// - Not on the grid: backdrops, things that float (lantern flies, wisps), tree house pieces, which go on a
    ///   giant tree instead (see <see cref="TreeHouse"/>), roofs, which go on a house (see <see cref="HouseRoof"/>),
    ///   wall pieces, which go on the great stump (see <see cref="WallPiece"/>), and the Burrow's loose ground clutter
    ///   (grass clumps, ferns, flowers, mushrooms, twigs and bark chips, see <see cref="IsLooseClutter"/>), which is
    ///   scattered by the pixel so it never lines up in rows.
    /// Plain C# on the sheet's own pixels, so it can be tested without the art package.
    /// </summary>
    public static class Footprints
    {
        static readonly Regex TallPiece = new Regex(@"^(vine|brown|wiggle|stout)_(base|middle|middle_knot|crown)$");
        static readonly Regex HousePiece = new Regex(@"^(vine_)?tree_(house|room|stairs)_");
        static readonly Regex RoofPiece = new Regex(@"^(.+)_roof_([a-z]+)$");
        static readonly Regex LoosePiece = new Regex(@"^burrow_(grass_|fern|bellworts|mushrooms|twig|bark_chips)");
        static readonly Regex TintedPiece = new Regex(@"^(.+)_([a-z]+)$");
        static readonly Regex BeamPiece = new Regex(@"^(.+)_beam_([a-z0-9]+)$");
        static readonly Regex RingPiece = new Regex(@"^(.+)_ring_([a-z0-9]+)$");
        static readonly Regex DitherPiece = new Regex(@"^(.+)_dither_(\d+)$");

        /// <summary>Base tiles for a piece, or 0 for one that doesn't sit on the map grid.</summary>
        public static int TilesWide(string name, bool backdrop, byte[] art, int sheetWidth, int sheetHeight, SheetSlice? slice)
        {
            if (backdrop || IsTreeHousePiece(name, out _, out _) || IsRoof(name, out _) || IsWallPiece(name) || IsLooseClutter(name)) return 0;
            if (IsBeam(name, out _) || IsRingBack(name, out _)) return 0;
            if (name.StartsWith("lantern_fly", StringComparison.Ordinal) || name.StartsWith("wisp", StringComparison.Ordinal)) return 0;
            if (name.StartsWith("young_", StringComparison.Ordinal) || name.StartsWith("stump_", StringComparison.Ordinal)) return 1;
            if (IsTreePiece(name)) return 2;
            return slice.HasValue && art != null ? Measured(art, sheetWidth, sheetHeight, slice.Value) : 1;
        }

        /// <summary>Tall and giant tree pieces (stacked trees use the same 2 tiles).</summary>
        public static bool IsTreePiece(string name) =>
            name.StartsWith("giant_", StringComparison.Ordinal) || TallPiece.IsMatch(name);

        /// <summary>Tree house pieces: tree_house_*, tree_room_*, tree_stairs_* (wood trunks) and vine_tree_* (the vine).</summary>
        public static bool IsTreeHousePiece(string name, out TreeHouse.Part part, out TreeFit fit)
        {
            var m = HousePiece.Match(name);
            part = TreeHouse.Part.House; fit = TreeFit.Wood;
            if (!m.Success) return false;
            fit = m.Groups[1].Success ? TreeFit.Vine : TreeFit.Wood;
            part = m.Groups[2].Value == "house" ? TreeHouse.Part.House : m.Groups[2].Value == "room" ? TreeHouse.Part.Room : TreeHouse.Part.Stairs;
            return true;
        }

        /// <summary>A piece that goes on the great stump's wall, like burrow_stump_ladder (see <see cref="WallPiece"/>).</summary>
        public static bool IsWallPiece(string name) => name.StartsWith("burrow_stump_", StringComparison.Ordinal);

        /// <summary>
        /// Small ground clutter off the burrow_nature sheet that goes anywhere by the pixel, not on the grid: grass
        /// clumps, ferns, bellworts, mushrooms, twigs and bark chips. Its rocks still root on the grid like boulders.
        /// </summary>
        public static bool IsLooseClutter(string name) => LoosePiece.IsMatch(name);

        /// <summary>
        /// The tall beam of a piece split in two, like burrow_log_beam_a: it goes on its base, burrow_log_support_a, in the
        /// base's prefab, so it can turn see-through while the base stays solid (see <see cref="SeeThrough"/>). It has no
        /// prefab of its own.
        /// </summary>
        public static bool IsBeam(string name, out string baseName)
        {
            var m = BeamPiece.Match(name ?? "");
            baseName = m.Success ? $"{m.Groups[1].Value}_support_{m.Groups[2].Value}" : null;
            return m.Success;
        }

        /// <summary>
        /// The far half of a base's ring, like burrow_log_ring_a for burrow_log_support_a: it goes in the base's prefab
        /// behind the beam, so the beam stands down inside the ring. It has no prefab of its own.
        /// </summary>
        public static bool IsRingBack(string name, out string baseName)
        {
            var m = RingPiece.Match(name ?? "");
            baseName = m.Success ? $"{m.Groups[1].Value}_support_{m.Groups[2].Value}" : null;
            return m.Success;
        }

        /// <summary>
        /// A part of a base kept in the base's own prefab (its beam or the far half of its ring, or a bridge's rail, see
        /// <see cref="Bridges"/>), so no prefab of its own.
        /// </summary>
        public static bool IsPartOfABase(string name, ICollection<string> names) =>
            (IsBeam(name, out var b) && names.Contains(b)) || (IsRingBack(name, out var r) && names.Contains(r)) ||
            (Bridges.IsRail(name, out var bridge) && names.Contains(bridge)) ||
            (IsDither(name, out var of, out _) && names.Contains(of));

        /// <summary>
        /// One step of a beam's dither, like burrow_log_beam_a_dither_08: the beam with only some of its pixels, in a
        /// checkered pattern, that its <see cref="SeeThrough"/> shows instead of blending it (see BellwortSheetImporter).
        /// It goes in the beam's prefab, so no prefab of its own.
        /// </summary>
        public static bool IsDither(string name, out string of, out int step)
        {
            var m = DitherPiece.Match(name ?? "");
            of = m.Success ? m.Groups[1].Value : null;
            step = m.Success ? int.Parse(m.Groups[2].Value) : 0;
            return m.Success;
        }

        /// <summary>A beam's dither steps, lightest first, or an empty array when it has none.</summary>
        public static Sprite[] DitherFor(string name, IDictionary<string, Sprite> sprites)
        {
            var steps = new Sprite[SeeThrough.DitherSteps - 1];
            for (int i = 0; i < steps.Length; i++)
                if (!sprites.TryGetValue(DitherName(name, i + 1), out steps[i])) return new Sprite[0];
            return steps;
        }

        /// <summary>The name of a beam's dither step, like burrow_log_beam_a_dither_08.</summary>
        public static string DitherName(string name, int step) => $"{name}_dither_{step:00}";

        /// <summary>The beam that goes on a base, like burrow_log_beam_a on burrow_log_support_a, or null.</summary>
        public static string BeamFor(string baseName, ICollection<string> names) => PartFor(baseName, "_beam_", names);

        /// <summary>The far half of a base's ring, like burrow_log_ring_a for burrow_log_support_a, or null.</summary>
        public static string RingBackFor(string baseName, ICollection<string> names) => PartFor(baseName, "_ring_", names);

        static string PartFor(string baseName, string part, ICollection<string> names)
        {
            int at = baseName.LastIndexOf("_support_", StringComparison.Ordinal);
            if (at < 0) return null;
            string name = baseName.Substring(0, at) + part + baseName.Substring(at + "_support_".Length);
            return names.Contains(name) ? name : null;
        }

        /// <summary>A building whose walls take wall pieces: the great stump (see <see cref="WallSeat"/>).</summary>
        public static bool TakesWallPieces(string name) => name == "burrow_stump";

        /// <summary>A roof that goes on top of a house, like burrow_home_roof_red; fits is the house size, burrow_home.</summary>
        public static bool IsRoof(string name, out string fits)
        {
            var m = RoofPiece.Match(name);
            fits = m.Success ? m.Groups[1].Value : null;
            return m.Success;
        }

        /// <summary>
        /// The size of a house that takes a roof: a piece like burrow_home_red whose sheet also has its roof,
        /// burrow_home_roof_red. Null for anything else.
        /// </summary>
        public static string RoofSeatSize(string name, ICollection<string> sliceNames)
        {
            if (IsRoof(name, out _)) return null;
            var m = TintedPiece.Match(name);
            if (!m.Success) return null;
            string size = m.Groups[1].Value;
            return sliceNames.Contains($"{size}_roof_{m.Groups[2].Value}") ? size : null;
        }

        /// <summary>The solid pixels in the 16 rows above the feet, as whole tiles (at least 1).</summary>
        public static int Measured(byte[] art, int sheetWidth, int sheetHeight, SheetSlice slice)
        {
            int feet = slice.Y + (slice.HasPivot ? slice.PivotY : slice.Height);
            int left = int.MaxValue, right = int.MinValue;
            for (int y = Math.Max(0, feet - 16); y < Math.Min(sheetHeight, feet); y++)
                for (int x = Math.Max(0, slice.X); x < Math.Min(sheetWidth, slice.X + slice.Width); x++)
                    if (art[(y * sheetWidth + x) * 4 + 3] == 255) { left = Math.Min(left, x); right = Math.Max(right, x); }
            if (right < left) return 1;
            return Math.Max(1, (int)Math.Round((right - left + 1) / 16.0, MidpointRounding.AwayFromZero));
        }
    }
}
