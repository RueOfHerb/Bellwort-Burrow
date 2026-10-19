using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace BellwortBurrow.EditorTools
{
    /// <summary>
    /// The art specs that code can check (art-1 and epic-1 in Notion), plus the fingerprint the Sync Ledger uses.
    /// Plain C# on the sheet's own pixels, so the rules can be tested without the art package.
    /// </summary>
    public static class ArtRules
    {
        /// <summary>The sigil's red, on the Glow layer of every home.</summary>
        public static readonly byte[] SigilRed = { 0xD0, 0x29, 0x40 };

        /// <summary>The contact shadow's navy, drawn part transparent on the ground around every home.</summary>
        public static readonly byte[] ShadowNavy = { 0x12, 0x1C, 0x34 };

        /// <summary>Slices that float instead of standing on the ground, so their pivot isn't at their feet.</summary>
        public static readonly HashSet<string> Floating = new HashSet<string> { "wisp" };

        const int MinSigilPixels = 20, MinShadowPixels = 40;

        /// <summary>The first 8 characters of the SHA1 of a file's bytes, as the Sync Ledger records it.</summary>
        public static string Fingerprint(byte[] data)
        {
            using (var sha = SHA1.Create())
            {
                byte[] hash = sha.ComputeHash(data);
                var text = new StringBuilder(8);
                for (int i = 0; i < 4; i++) text.Append(hash[i].ToString("x2"));
                return text.ToString();
            }
        }

        /// <summary>Cottages and tree house upper rooms carry the sigil.</summary>
        public static bool NeedsSigil(string sliceName) =>
            sliceName.Contains("_cottage_") || sliceName.Contains("tree_room_");

        /// <summary>Cottages and ground tree houses sit on a contact shadow.</summary>
        public static bool NeedsShadow(string sliceName) =>
            sliceName.Contains("_cottage_") || sliceName.Contains("tree_house_");

        /// <summary>Upper rooms and stairs hang on a giant tree's trunk, so their pivot is at the join they sit on, not at feet.</summary>
        public static bool OnTrunk(string sliceName) =>
            sliceName.Contains("tree_room_") || sliceName.Contains("tree_stairs_");

        /// <summary>The pivot sits at the bottom middle, where the thing meets the ground (a base may reach a few pixels lower).</summary>
        public static bool PivotAtFeet(SheetSlice slice) =>
            slice.HasPivot && Math.Abs(slice.PivotX * 2 - slice.Width) <= 2 &&
            slice.PivotY >= slice.Height - 4 && slice.PivotY <= slice.Height;

        /// <summary>Pixels of one color inside a slice. With partOnly, only part-transparent pixels count (shadows).</summary>
        public static int CountColor(byte[] rgba, int sheetWidth, int sheetHeight, SheetSlice slice, byte[] rgb, bool partOnly)
        {
            if (rgba == null) return 0;
            int count = 0;
            int x0 = Math.Max(0, slice.X), y0 = Math.Max(0, slice.Y);
            int x1 = Math.Min(sheetWidth, slice.X + slice.Width), y1 = Math.Min(sheetHeight, slice.Y + slice.Height);
            for (int y = y0; y < y1; y++)
            {
                for (int x = x0; x < x1; x++)
                {
                    int i = (y * sheetWidth + x) * 4;
                    byte a = rgba[i + 3];
                    if (a == 0 || (partOnly && a == 255)) continue;
                    if (rgba[i] == rgb[0] && rgba[i + 1] == rgb[1] && rgba[i + 2] == rgb[2]) count++;
                }
            }
            return count;
        }

        /// <summary>Pairs of slices that overlap. Slices that only touch along an edge don't count.</summary>
        public static List<string> Overlaps(IReadOnlyList<SheetSlice> slices)
        {
            var pairs = new List<string>();
            for (int i = 0; i < slices.Count; i++)
            {
                for (int j = i + 1; j < slices.Count; j++)
                {
                    SheetSlice a = slices[i], b = slices[j];
                    if (a.X < b.X + b.Width && b.X < a.X + a.Width && a.Y < b.Y + b.Height && b.Y < a.Y + a.Height)
                        pairs.Add($"{a.Name} and {b.Name}");
                }
            }
            return pairs;
        }

        static readonly Regex TreePiece = new Regex(@"^(.+)_(base|middle|middle_knot|crown)$");
        static readonly Regex HousePiece = new Regex(@"^((?:vine_)?tree)_(house|stairs|room)_(.+)$");
        static readonly string[] TreeOrder = { "base", "middle", "middle_knot", "crown" };
        static readonly string[] HouseOrder = { "house", "stairs", "room" };

        /// <summary>
        /// Pieces that go together are laid out on the sheet as if painted together: a tree's base, middles and crown
        /// (bottom to top, the way they stack) and a tree house set's ground house, stairs and upper room, each one
        /// standing straight on the one below it (touching, no gap) on one center line (the pivot column).
        /// Returns a line for every piece that doesn't.
        /// </summary>
        public static List<string> StandTogether(IReadOnlyList<SheetSlice> slices)
        {
            var groups = new Dictionary<string, SortedDictionary<int, SheetSlice>>();
            foreach (var slice in slices)
            {
                var tree = TreePiece.Match(slice.Name);
                var house = HousePiece.Match(slice.Name);
                string key; int rank;
                if (house.Success) { key = $"{house.Groups[1].Value}_{house.Groups[3].Value} set"; rank = Array.IndexOf(HouseOrder, house.Groups[2].Value); }
                else if (tree.Success) { key = tree.Groups[1].Value; rank = Array.IndexOf(TreeOrder, tree.Groups[2].Value); }
                else continue;
                if (!groups.TryGetValue(key, out var group)) groups[key] = group = new SortedDictionary<int, SheetSlice>();
                group[rank] = slice;
            }
            var problems = new List<string>();
            foreach (var pair in groups)
            {
                if (pair.Value.Count < 2) continue;
                SheetSlice? below = null;
                foreach (var piece in pair.Value.Values)
                {
                    if (below.HasValue)
                    {
                        var b = below.Value;
                        int column = piece.X + (piece.HasPivot ? piece.PivotX : piece.Width / 2);
                        int belowColumn = b.X + (b.HasPivot ? b.PivotX : b.Width / 2);
                        if (column != belowColumn || piece.Y + piece.Height != b.Y)
                            problems.Add($"{piece.Name} doesn't stand straight on {b.Name}");
                    }
                    below = piece;
                }
            }
            return problems;
        }

        /// <summary>Every rule broken on one sheet, as short lines. Empty when the sheet follows the specs.</summary>
        public static List<string> Check(string sheetName, int width, int height, byte[] art, byte[] glow,
                                         IReadOnlyList<SheetSlice> slices, bool tileSheet)
        {
            var problems = new List<string>();
            foreach (var pair in Overlaps(slices))
                problems.Add($"{sheetName}: slices {pair} overlap");
            if (!tileSheet)
                foreach (var line in StandTogether(slices))
                    problems.Add($"{sheetName}: {line} (pieces that go together are laid out stacked as if painted together, touching, on one center line)");

            foreach (var slice in slices)
            {
                if (!tileSheet && !Floating.Contains(slice.Name) && !OnTrunk(slice.Name) && !PivotAtFeet(slice))
                    problems.Add($"{sheetName}: {slice.Name} has its pivot away from its feet (set it to the bottom middle)");

                if (NeedsSigil(slice.Name) && CountColor(glow, width, height, slice, SigilRed, false) < MinSigilPixels)
                    problems.Add($"{sheetName}: {slice.Name} has no sigil on its Glow layer");
                if (NeedsShadow(slice.Name) && CountColor(art, width, height, slice, ShadowNavy, true) < MinShadowPixels)
                    problems.Add($"{sheetName}: {slice.Name} has no contact shadow at its base, so it will look like it hovers");
            }
            return problems;
        }
    }
}
