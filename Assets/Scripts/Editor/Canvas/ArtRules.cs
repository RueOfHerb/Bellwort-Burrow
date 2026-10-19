using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

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

        /// <summary>Every rule broken on one sheet, as short lines. Empty when the sheet follows the specs.</summary>
        public static List<string> Check(string sheetName, int width, int height, byte[] art, byte[] glow,
                                         IReadOnlyList<SheetSlice> slices, bool tileSheet)
        {
            var problems = new List<string>();
            foreach (var pair in Overlaps(slices))
                problems.Add($"{sheetName}: slices {pair} overlap");

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
