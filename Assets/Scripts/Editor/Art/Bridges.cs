using System;
using System.Collections.Generic;
using System.Linq;
using BellwortBurrow.Systems.World;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace BellwortBurrow.EditorTools
{
    /// <summary>
    /// Bridges the player walks across (see <see cref="Bridge"/>), and how setup puts one together.
    /// - A bridge's sheet has three slices the same size with the same feet: the bridge itself (deck, legs, the shade
    ///   under it), &lt;bridge&gt;_near_rail and &lt;bridge&gt;_far_rail (each rail with its corner posts and lanterns).
    ///   The rails don't get prefabs of their own: they go on the bridge, cut into strips that sort by where they stand.
    /// - Its shape is known from how it was drawn (<see cref="Shapes"/>): a deck on the 45 degree from northwest to
    ///   southeast, flat over the water and ramping down to the banks at both ends. Setup gives the prefab a trigger over
    ///   the deck (whoever is on it walks over the water), a solid near side from the deck's edge down to the ground (no
    ///   stepping off it, and no walking onto its face from outside), a solid line along the far rail, and open ends.
    /// Plain C# apart from <see cref="Dress"/>, so the shapes can be tested without the art package.
    /// </summary>
    public static class Bridges
    {
        public const string NearRailSuffix = "_near_rail";
        public const string FarRailSuffix = "_far_rail";

        /// <summary>
        /// A deck on the 45 degree, northwest to southeast, as drawn: along it k runs northwest (-) to southeast (+), across
        /// it s runs southwest (-) to northeast (+), one pixel each way per step of either, and a point on the ground sits
        /// at Middle + (k + s, s - k) pixels from the feet (y up). The deck stands Height pixels over the ground for
        /// |k| up to Span, and ramps down to the ground at |k| = Length.
        /// </summary>
        public sealed class DiagonalDeck
        {
            public readonly Vector2 Middle;
            public readonly float Length, HalfWidth, Span, Height;

            public DiagonalDeck(Vector2 middle, float length, float halfWidth, float span, float height)
            {
                Middle = middle; Length = length; HalfWidth = halfWidth; Span = span; Height = height;
            }

            public float DeckHeight(float k)
            {
                float a = Math.Abs(k);
                if (a <= Span) return Height;
                return Math.Max(0f, Height * (Length - a) / (Length - Span));
            }

            /// <summary>A point on the deck (z = its height) or the ground (z = 0), in pixels from the feet, y up.</summary>
            public Vector2 At(float k, float s, float z) => Middle + new Vector2(k + s, s - k + z);

            Vector2 OnDeck(float k, float s) => At(k, s, DeckHeight(k));

            /// <summary>The k values where the deck's line bends: the ends, and where the ramps meet the flat.</summary>
            float[] Knots(float end) => new[] { -end, -Span, Span, end };

            /// <summary>The whole deck as seen from above, edge to edge and end to end.</summary>
            public Vector2[] Deck() => Band(-HalfWidth, HalfWidth);

            /// <summary>
            /// Where a walker counts as on the bridge: end to end, and from <paramref name="gap"/> inside the near side's
            /// line to <paramref name="gap"/> inside the far side's, so someone outside pressed against a side never counts.
            /// </summary>
            public Vector2[] Walkway(float nearInset, float farInset, float gap) =>
                Band(-HalfWidth + nearInset + gap, HalfWidth - farInset - gap);

            Vector2[] Band(float nearS, float farS)
            {
                var points = Knots(Length).Select(k => OnDeck(k, farS)).ToList();
                points.AddRange(Knots(Length).Reverse().Select(k => OnDeck(k, nearS)));
                return points.ToArray();
            }

            /// <summary>
            /// The near side: from a line just inside the deck's edge (inset, so a player's feet stay on the planks) down
            /// to the ground under the edge. Solid from both sides; open at the ends.
            /// </summary>
            public Vector2[] NearSide(float inset, float endGap)
            {
                float end = Length - endGap;
                var points = Knots(end).Select(k => OnDeck(k, -HalfWidth + inset)).ToList();
                points.Add(At(end, -HalfWidth, 0f));
                points.Add(At(-end, -HalfWidth, 0f));
                return points.ToArray();
            }

            /// <summary>The far side: a line just inside the far edge, open at the ends.</summary>
            public Vector2[] FarSide(float inset, float endGap) =>
                Knots(Length - endGap).Select(k => OnDeck(k, HalfWidth - inset)).ToArray();

            /// <summary>Where a rail stands on the deck, s across (near -, far +), end to end.</summary>
            public Vector2[] RailFoot(float s) => Knots(Length).Select(k => OnDeck(k, s)).ToArray();
        }

        /// <summary>The bridges setup knows how to put together, by slice name, in the art's own pixels.</summary>
        public static readonly IReadOnlyDictionary<string, DiagonalDeck> Shapes = new Dictionary<string, DiagonalDeck>
        {
            // tmp/art-2.31 trestle bridge/trestle.py: middle 1 px left of and 61 px above its feet, K 48, S 9, SPAN 30, Z 13.
            ["burrow_bridge_trestle"] = new DiagonalDeck(new Vector2(-1f, 61f), 48f, 9f, 30f, 13f),
        };

        /// <summary>How far in from the near edge a player's feet stop, in steps across (about 3 px).</summary>
        public const float NearInset = 1.5f;
        /// <summary>How far in from the far edge a player's feet stop, in steps across.</summary>
        public const float FarInset = 1f;
        /// <summary>Where the rails stand, in from each edge.</summary>
        public const float RailInset = 0.5f;
        /// <summary>How far inside the side lines the walkway stops, in steps across (1 px up or down).</summary>
        public const float WalkwayGap = 0.5f;
        /// <summary>The open ends: how far short of each end the sides stop, in steps along (the corner posts stand there).</summary>
        public const float EndGap = 2f;
        /// <summary>How wide each rail strip is, in pixels.</summary>
        public const int StripWidth = 4;

        public static bool IsBridge(string name) => name != null && Shapes.ContainsKey(name);

        /// <summary>A rail slice of a bridge, like burrow_bridge_trestle_near_rail; bridge is the bridge's name.</summary>
        public static bool IsRail(string name, out string bridge)
        {
            bridge = null;
            if (name == null) return false;
            foreach (var suffix in new[] { NearRailSuffix, FarRailSuffix })
                if (name.EndsWith(suffix, StringComparison.Ordinal))
                {
                    bridge = name.Substring(0, name.Length - suffix.Length);
                    return IsBridge(bridge);
                }
            return false;
        }

        /// <summary>
        /// The strips of a slice that have any pixels on the art or the glow: the first column of each, from the slice's
        /// left edge, <paramref name="width"/> columns apart.
        /// </summary>
        public static int[] Strips(byte[] art, byte[] glow, int sheetWidth, int sheetHeight, SheetSlice slice, int width)
        {
            var strips = new List<int>();
            for (int left = 0; left < slice.Width; left += width)
            {
                bool any = false;
                for (int x = slice.X + left; x < Math.Min(slice.X + left + width, Math.Min(slice.X + slice.Width, sheetWidth)) && !any; x++)
                    for (int y = Math.Max(0, slice.Y); y < Math.Min(sheetHeight, slice.Y + slice.Height) && !any; y++)
                    {
                        int i = (y * sheetWidth + x) * 4 + 3;
                        any = (art != null && art[i] > 0) || (glow != null && glow[i] > 0);
                    }
                if (any) strips.Add(left);
            }
            return strips.ToArray();
        }

        static Vector2[] Units(IEnumerable<Vector2> pixels) => pixels.Select(p => p / ArtLibrary.PixelsPerUnit).ToArray();

        /// <summary>
        /// Puts a bridge prefab together: the deck flat under everything that stands, its rails as strips, the lanterns'
        /// lights, the colliders, and its footprint on the grid.
        /// </summary>
        public static void Dress(GameObject root, SpriteRenderer renderer, string name, IReadOnlyDictionary<string, Sprite> sprites,
                                 AsepriteSheet sheet, IReadOnlyDictionary<string, SheetSlice> slices, Material lit, Material unlit)
        {
            var shape = Shapes[name];

            // The deck lies flat: under the player wherever they are, so it keeps no sorting group of its own.
            var group = root.GetComponent<SortingGroup>();
            if (group != null) Object.DestroyImmediate(group);
            renderer.sortingOrder = Bridge.DeckSortingOrder;
            foreach (Transform child in root.transform)
                if (child.TryGetComponent<SpriteRenderer>(out var childRenderer)) childRenderer.sortingOrder = Bridge.DeckSortingOrder + 1;

            var bridge = root.AddComponent<Bridge>();
            bridge.stripWidth = StripWidth;
            bridge.litMaterial = lit;
            bridge.unlitMaterial = unlit;
            bridge.nearRail = MakeRail(name + NearRailSuffix, shape.RailFoot(-shape.HalfWidth + RailInset), sprites, sheet, slices);
            bridge.farRail = MakeRail(name + FarRailSuffix, shape.RailFoot(shape.HalfWidth - RailInset), sprites, sheet, slices);

            // The lanterns on the rails light the deck, from the bridge (the rails share its feet).
            if (sheet != null)
                foreach (var (suffix, label) in new[] { (NearRailSuffix, "Near"), (FarRailSuffix, "Far") })
                    if (slices.TryGetValue(name + suffix, out var railSlice))
                    {
                        var spots = GlowLights.Find(sheet, railSlice);
                        for (int i = 0; i < spots.Count; i++) { var spot = spots[i]; spot.Name = $"{label} {spot.Name}"; spots[i] = spot; }
                        GlowLights.AddLights(root, spots);
                    }

            var deck = root.AddComponent<PolygonCollider2D>();
            deck.isTrigger = true;
            deck.pathCount = 1;
            deck.SetPath(0, Units(shape.Walkway(NearInset, FarInset, WalkwayGap)));

            var near = root.AddComponent<PolygonCollider2D>();
            near.pathCount = 1;
            near.SetPath(0, Units(shape.NearSide(NearInset, EndGap)));

            var far = root.AddComponent<EdgeCollider2D>();
            far.points = Units(shape.FarSide(FarInset, EndGap));

            SheetSlice? footSlice = slices.TryGetValue(name, out var found) ? found : (SheetSlice?)null;
            int tiles = Footprints.TilesWide(name, false, sheet?.Art, sheet?.Width ?? 0, sheet?.Height ?? 0, footSlice);
            if (tiles > 0) root.AddComponent<GridFootprint>().tilesWide = tiles;
        }

        static Bridge.Rail MakeRail(string railName, Vector2[] footPixels, IReadOnlyDictionary<string, Sprite> sprites,
                                    AsepriteSheet sheet, IReadOnlyDictionary<string, SheetSlice> slices)
        {
            var rail = new Bridge.Rail { foot = Units(footPixels) };
            sprites.TryGetValue(railName, out rail.sprite);
            sprites.TryGetValue(railName + "_glow", out rail.glow);
            if (sheet != null && slices.TryGetValue(railName, out var slice))
                rail.strips = Strips(sheet.Art, sheet.HasGlow ? sheet.Glow : null, sheet.Width, sheet.Height, slice, StripWidth);
            else if (rail.sprite != null)
                rail.strips = Enumerable.Range(0, Mathf.CeilToInt(rail.sprite.rect.width / StripWidth)).Select(i => i * StripWidth).ToArray();
            return rail;
        }
    }
}
