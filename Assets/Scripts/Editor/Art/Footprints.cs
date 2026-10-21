using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using BellwortBurrow.Systems.World;

namespace BellwortBurrow.EditorTools
{
    /// <summary>
    /// How many base tiles each placeable piece is rooted on, where it meets the map (see <see cref="GridFootprint"/>).
    /// - Young trees and stumps: 1 tile. Tall and giant trees (and their pieces): 2 tiles.
    /// - Everything else: the tiles under its front, from the art itself: how wide it is in the tile above its feet,
    ///   in whole tiles (at least 1). So a cottage roots on 5 or 6 tiles and a boulder on 2.
    /// - Not on the grid: backdrops, things that float (lantern flies, wisps), tree house pieces, which go on a
    ///   giant tree instead (see <see cref="TreeHouse"/>), and roofs, which go on a house (see <see cref="HouseRoof"/>).
    /// Plain C# on the sheet's own pixels, so it can be tested without the art package.
    /// </summary>
    public static class Footprints
    {
        static readonly Regex TallPiece = new Regex(@"^(vine|brown|wiggle|stout)_(base|middle|middle_knot|crown)$");
        static readonly Regex HousePiece = new Regex(@"^(vine_)?tree_(house|room|stairs)_");
        static readonly Regex RoofPiece = new Regex(@"^(.+)_roof_([a-z]+)$");
        static readonly Regex TintedPiece = new Regex(@"^(.+)_([a-z]+)$");

        /// <summary>Base tiles for a piece, or 0 for one that doesn't sit on the map grid.</summary>
        public static int TilesWide(string name, bool backdrop, byte[] art, int sheetWidth, int sheetHeight, SheetSlice? slice)
        {
            if (backdrop || IsTreeHousePiece(name, out _, out _) || IsRoof(name, out _)) return 0;
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
