using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace BellwortBurrow.EditorTools
{
    /// <summary>
    /// Which placeable pieces the player walks round instead of through, and the shape of the ground each one stands on.
    /// - Solid: the great stump, the Burrow's homes and cottages, its mossy rocks, and the fallen log's supports and the
    ///   benches beside them. Not solid: roofs (they sit on a
    ///   home), the stump's wall pieces (its ladder, lantern and foot growth) and the loose ground clutter.
    /// - The shape is the piece's footprint, worked out from the sheet's own pixels (<see cref="AsepriteSheet.Solid"/>,
    ///   which leaves out shadows, steps, stairs and foot growth): its front edge follows the lowest solid pixel of every
    ///   column, so it hugs roots and bulges, and its back edge mirrors the front about the footprint's middle, so a round
    ///   stump stands on a round patch of ground. Eaves and other overhangs are left out (<see cref="Overhang"/>). A piece whose front is flat still gets a patch at least half as deep as
    ///   it is wide (<see cref="FootprintDepth"/>), the view's slant: the stump's cut top is about half as tall as wide.
    /// Plain C# on the sheet's own pixels, so it can be tested without the art package.
    /// </summary>
    public static class Colliders
    {
        static readonly Regex SolidPiece = new Regex(@"^burrow_(stump$|nook_|home_|cottage_|mossy_rock|log_support|log_bench)");

        /// <summary>How deep the ground under a piece is, as a share of its width, at the least.</summary>
        public const float FootprintDepth = 0.5f;

        /// <summary>
        /// A front edge that steps up more than this in one column (pixels) is an overhang, like a roof's eaves, not
        /// ground the piece stands on. The stump's roots step up 43 px at most; the cottage's eaves 85 px.
        /// </summary>
        public const int Overhang = 48;

        /// <summary>Pixels between the points along the front edge, before straight runs are merged.</summary>
        public const int Step = 4;

        /// <summary>Whether the player walks round this piece.</summary>
        public static bool IsSolid(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (Footprints.IsRoof(name, out _) || Footprints.IsWallPiece(name) || Footprints.IsLooseClutter(name)) return false;
            return SolidPiece.IsMatch(name);
        }

        /// <summary>
        /// Round pieces: their collider is a box with its corners cut at 45 degrees, so the player slides round them.
        /// Everything else solid (homes, cottages, benches, the log supports, whose stone rings run nearly as wide at
        /// the front as in the middle) stands on a plain box (Jocelyn, Oct 27: rectangles and polygons with 90 and 45
        /// degree corners, not traced outlines; the supports' cut corners let the player onto the ring's stones).
        /// </summary>
        static readonly Regex RoundPiece = new Regex(@"^burrow_(stump$|mossy_rock)");

        public static bool IsRound(string name) => !string.IsNullOrEmpty(name) && RoundPiece.IsMatch(name);

        /// <summary>
        /// The footprint of a slice, in pixels from its feet (its pivot, or the middle of its bottom edge), x to the
        /// right and y up, going round once: a box as wide as the piece's base (eaves and other overhangs left out,
        /// see <see cref="Overhang"/>), its front on the lowest solid row, at least half as deep as it is wide
        /// (<see cref="FootprintDepth"/>). A <paramref name="round"/> piece's box has its four corners cut at 45
        /// degrees, by as much as its front edge curves back at the sides, so it's made of squares and triangles only.
        /// Null when the slice has no solid pixels.
        /// </summary>
        public static List<Vector2> Footprint(bool[] solid, int sheetWidth, int sheetHeight, SheetSlice slice, bool round = false)
        {
            if (solid == null) return null;
            int feet = slice.Y + (slice.HasPivot ? slice.PivotY : slice.Height);
            int pivotX = slice.X + (slice.HasPivot ? slice.PivotX : slice.Width / 2);
            int top = Math.Max(0, slice.Y), bottom = Math.Min(sheetHeight, slice.Y + slice.Height);
            int left = Math.Max(0, slice.X), right = Math.Min(sheetWidth, slice.X + slice.Width);

            // The front edge: the bottom of the lowest solid pixel in each column (image rows, down).
            int columns = Math.Max(0, right - left);
            var front = new int[columns];
            int lowest = -1;
            for (int i = 0; i < columns; i++)
            {
                front[i] = -1;
                for (int y = bottom - 1; y >= top; y--)
                    if (solid[y * sheetWidth + left + i]) { front[i] = y + 1; break; }
                if (front[i] >= 0 && (lowest < 0 || front[i] > front[lowest])) lowest = i;
            }
            if (lowest < 0) return null;

            // The base: the columns either side of the lowest one, as far as the edge carries on without a gap or an
            // overhang (eaves, a jutting roof), where it steps up more than Overhang in one column.
            int a = lowest, b = lowest;
            while (a > 0 && front[a - 1] >= 0 && Math.Abs(front[a - 1] - front[a]) <= Overhang) a--;
            while (b < columns - 1 && front[b + 1] >= 0 && Math.Abs(front[b + 1] - front[b]) <= Overhang) b++;
            int highest = int.MaxValue;
            for (int i = a; i <= b; i++) highest = Math.Min(highest, front[i]);

            float xl = left + a - pivotX, xr = left + b + 1 - pivotX;
            float width = xr - xl;
            float y0 = feet - front[lowest];
            float rise = front[lowest] - highest;
            float depth = Mathf.Max(1f, Mathf.Round(Mathf.Max(width * FootprintDepth, round ? 2f * rise : 0f)));
            float y1 = y0 + depth;

            float cut = round ? Mathf.Round(Mathf.Min(rise, Mathf.Min(width, depth) / 2f - 1f)) : 0f;
            if (cut < 1f)
                return new List<Vector2> { new(xl, y0), new(xr, y0), new(xr, y1), new(xl, y1) };
            return new List<Vector2>
            {
                new(xl, y0 + cut), new(xl + cut, y0), new(xr - cut, y0), new(xr, y0 + cut),
                new(xr, y1 - cut), new(xr - cut, y1), new(xl + cut, y1), new(xl, y1 - cut)
            };
        }

        /// <summary>The front of a footprint, left to right: the points nearer the camera than its middle.</summary>
        public static List<Vector2> FrontOf(List<Vector2> shape)
        {
            if (shape == null || shape.Count == 0) return new List<Vector2>();
            float middle = (shape.Min(p => p.y) + shape.Max(p => p.y)) / 2f;
            return shape.Where(p => p.y < middle).OrderBy(p => p.x).ToList();
        }

        /// <summary>Douglas-Peucker: drops points that lie within <paramref name="tolerance"/> of a straight run.</summary>
        internal static List<Vector2> Simplify(List<Vector2> points, float tolerance)
        {
            if (points.Count < 3) return new List<Vector2>(points);
            var keep = new bool[points.Count];
            keep[0] = keep[points.Count - 1] = true;
            var stack = new Stack<(int, int)>();
            stack.Push((0, points.Count - 1));
            while (stack.Count > 0)
            {
                var (a, b) = stack.Pop();
                float worst = 0; int at = -1;
                for (int i = a + 1; i < b; i++)
                {
                    float d = Distance(points[i], points[a], points[b]);
                    if (d > worst) { worst = d; at = i; }
                }
                if (at >= 0 && worst > tolerance) { keep[at] = true; stack.Push((a, at)); stack.Push((at, b)); }
            }
            var result = new List<Vector2>();
            for (int i = 0; i < points.Count; i++) if (keep[i]) result.Add(points[i]);
            return result;
        }

        static float Distance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float length = ab.magnitude;
            if (length < 1e-6f) return (p - a).magnitude;
            return Mathf.Abs(ab.x * (a.y - p.y) - ab.y * (a.x - p.x)) / length;
        }

        /// <summary>
        /// Ground tiles that block the player: the water (Burrow Water). Its tiles carry a physics shape made of
        /// quarters (<see cref="QuarterShape"/>), so a slope piece cuts at 45 degrees.
        /// </summary>
        public static bool IsBlockingGroundSlice(string sliceName) =>
            !string.IsNullOrEmpty(sliceName) && sliceName.StartsWith("water_", StringComparison.Ordinal);

        /// <summary>A raised ground's face set (bank_face_set, cliff_face_set and the rest of GroundAutoTiles.Faces), whose tiles block by <see cref="FaceShape"/>.</summary>
        public static bool IsFaceSlice(string sliceName) =>
            Array.Exists(GroundAutoTiles.Faces, f => f.slice == sliceName);

        /// <summary>A ramp's slice (GroundAutoTiles.Ramps), whose tiles block by <see cref="CutShape"/>: everything but the walkway.</summary>
        public static bool IsRampSlice(string sliceName) =>
            Array.Exists(GroundAutoTiles.Ramps, r => r.slice == sliceName);

        // ---- Tile shapes: every edge level, upright or at 45 degrees -------------------------------------------------

        /// <summary>
        /// A ground tile's physics shape from its picture, made of its four quarters (8 px squares for a 16 px tile), each
        /// whole, empty, or cut corner to corner into a triangle, the way the ground sets are drawn (a slope cuts a
        /// tile corner to corner, through two quarters). So the shapes only have level, upright and
        /// 45 degree edges, and the tiles' merged collider runs in clean straight lines. A ragged edge or a pebble or two
        /// past the edge doesn't move it. <paramref name="opaque"/> says whether pixel (x, y) is drawn, y counted from the
        /// tile's top. The shapes are in pixels from the tile's bottom left corner, y up.
        /// </summary>
        public static List<Vector2[]> QuarterShape(Func<int, int, bool> opaque, int size)
        {
            int q = size / 2;
            var shapes = new List<Vector2[]>();
            for (int qy = 0; qy < 2; qy++)
                for (int qx = 0; qx < 2; qx++)
                {
                    // the quarter's corners, y up
                    float x0 = qx * q, x1 = x0 + q, y1 = size - qy * q, y0 = y1 - q;
                    int all = 0, ul = 0, lr = 0, ur = 0, ll = 0, nul = 0, nlr = 0, nur = 0, nll = 0;
                    for (int y = 0; y < q; y++)
                        for (int x = 0; x < q; x++)
                        {
                            bool on = opaque(qx * q + x, qy * q + y);
                            if (on) all++;
                            // pixel centre against the quarter's diagonals (y down inside the quarter)
                            float fx = x + 0.5f, fy = y + 0.5f;
                            if (fx + fy < q) { nul++; if (on) ul++; } else if (fx + fy > q) { nlr++; if (on) lr++; }
                            if (fx > fy) { nur++; if (on) ur++; } else if (fx < fy) { nll++; if (on) ll++; }
                        }
                    float cover = all / (float)(q * q);
                    if (cover >= 0.75f) { shapes.Add(new[] { new Vector2(x0, y0), new Vector2(x1, y0), new Vector2(x1, y1), new Vector2(x0, y1) }); continue; }
                    if (cover <= 0.25f) continue;
                    // a triangle: one half of the quarter drawn and the other not
                    float fUL = ul / (float)nul, fLR = lr / (float)nlr, fUR = ur / (float)nur, fLL = ll / (float)nll;
                    var best = new[]
                    {
                        (fUL - fLR, new[] { new Vector2(x0, y0), new Vector2(x1, y1), new Vector2(x0, y1) }),   // upper left
                        (fLR - fUL, new[] { new Vector2(x0, y0), new Vector2(x1, y0), new Vector2(x1, y1) }),   // lower right
                        (fUR - fLL, new[] { new Vector2(x0, y1), new Vector2(x1, y0), new Vector2(x1, y1) }),   // upper right
                        (fLL - fUR, new[] { new Vector2(x0, y0), new Vector2(x1, y0), new Vector2(x0, y1) }),   // lower left
                    }.OrderByDescending(b => b.Item1).First();
                    if (best.Item1 >= 0.5f) shapes.Add(best.Item2);
                    else if (cover >= 0.5f) shapes.Add(new[] { new Vector2(x0, y0), new Vector2(x1, y0), new Vector2(x1, y1), new Vector2(x0, y1) });
                }
            return shapes;
        }

        /// <summary>
        /// A face piece's physics shape from its key (<see cref="FaceTile.keys"/>), in pixels from the tile's bottom
        /// left corner, y up. A flat face blocks its whole tile. A 45 degree column blocks between its lip and its foot:
        /// its top cell below the lip, which crosses it corner to corner, its bottom cell above the foot, its middle cells
        /// whole. So a 45 degree run of bank blocks along a straight 45 degree line, not in steps.
        /// </summary>
        public static List<Vector2[]> FaceShape(string key, int size)
        {
            Vector2 bl = new Vector2(0, 0), br = new Vector2(size, 0), tr = new Vector2(size, size), tl = new Vector2(0, size);
            var whole = new List<Vector2[]> { new[] { bl, br, tr, tl } };
            if (string.IsNullOrEmpty(key) || key.Length < 2 || key[0] == 'F') return whole;
            bool down = key[0] == 'D';      // down to the east: the lip runs from the top left to the bottom right
            char place = key[1];
            if (place == '0') return new List<Vector2[]> { down ? new[] { bl, br, tl } : new[] { bl, br, tr } };   // below the lip
            if (place == '3') return new List<Vector2[]> { down ? new[] { tl, br, tr } : new[] { tl, bl, tr } };   // above the foot
            return whole;
        }

        /// <summary>
        /// The physics shape of a ramp's tile: its solid pixels, in bands. Down each column of the tile the solid pixels
        /// come in runs; runs that line up from column to column make a band, and each band is a polygon whose top and
        /// bottom edges follow the runs as straight lines (the pixel steps smoothed out to within a pixel), so a walker
        /// slides along a ramp's edge rather than catching on it. <paramref name="solid"/> says whether pixel (x, y)
        /// blocks, y counted from the tile's top. The shapes are in pixels from the tile's bottom left corner, y up.
        /// </summary>
        public static List<Vector2[]> CutShape(Func<int, int, bool> solid, int size)
        {
            var shapes = new List<Vector2[]>();
            var open = new List<(List<Vector2> top, List<Vector2> bottom, int a, int b)>();
            void Close((List<Vector2> top, List<Vector2> bottom, int a, int b) band)
            {
                var top = Simplify(band.top, 0.9f);
                var bottom = Simplify(band.bottom, 0.9f);
                // round the band: west to east along its top, then back west along its bottom, flipped to y up
                var polygon = new List<Vector2>();
                foreach (var p in top) polygon.Add(new Vector2(p.x, size - p.y));
                for (int i = bottom.Count - 1; i >= 0; i--) polygon.Add(new Vector2(bottom[i].x, size - bottom[i].y));
                var clean = Tidy(polygon);
                if (clean.Count < 3 || Mathf.Abs(Area(clean)) < 2f) return;
                if (Area(clean) < 0) clean.Reverse();        // counter-clockwise, like the quarter shapes
                shapes.Add(clean.ToArray());
            }
            for (int x = 0; x <= size; x++)
            {
                var runs = new List<(int a, int b)>();
                if (x < size)
                    for (int y = 0; y < size; y++)
                        if (solid(x, y))
                        {
                            int a = y;
                            while (y + 1 < size && solid(x, y + 1)) y++;
                            runs.Add((a, y + 1));
                        }
                var next = new List<(List<Vector2> top, List<Vector2> bottom, int a, int b)>();
                var used = new bool[runs.Count];
                foreach (var band in open)
                {
                    int match = -1, matches = 0;
                    for (int i = 0; i < runs.Count; i++)
                        if (runs[i].a < band.b && runs[i].b > band.a) { match = i; matches++; }
                    bool alone = matches == 1 && !used[match] && open.Count(o => runs[match].a < o.b && runs[match].b > o.a) == 1;
                    if (alone)
                    {
                        var r = runs[match];
                        band.top.Add(new Vector2(x, r.a)); band.top.Add(new Vector2(x + 1, r.a));
                        band.bottom.Add(new Vector2(x, r.b)); band.bottom.Add(new Vector2(x + 1, r.b));
                        next.Add((band.top, band.bottom, r.a, r.b));
                        used[match] = true;
                    }
                    else Close(band);
                }
                for (int i = 0; i < runs.Count; i++)
                    if (!used[i])
                    {
                        var r = runs[i];
                        next.Add((new List<Vector2> { new Vector2(x, r.a), new Vector2(x + 1, r.a) },
                                  new List<Vector2> { new Vector2(x, r.b), new Vector2(x + 1, r.b) }, r.a, r.b));
                    }
                open = next;
            }
            foreach (var band in open) Close(band);
            return shapes;
        }

        /// <summary>A polygon without repeated points or points on a straight line between their neighbours.</summary>
        static List<Vector2> Tidy(List<Vector2> polygon)
        {
            var points = new List<Vector2>();
            foreach (var p in polygon)
                if (points.Count == 0 || (p - points[points.Count - 1]).sqrMagnitude > 1e-6f) points.Add(p);
            while (points.Count > 1 && (points[0] - points[points.Count - 1]).sqrMagnitude <= 1e-6f) points.RemoveAt(points.Count - 1);
            bool changed = true;
            while (changed && points.Count >= 3)
            {
                changed = false;
                for (int i = 0; i < points.Count; i++)
                {
                    var a = points[(i + points.Count - 1) % points.Count];
                    var b = points[i];
                    var c = points[(i + 1) % points.Count];
                    float cross = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
                    if (Mathf.Abs(cross) < 1e-4f) { points.RemoveAt(i); changed = true; break; }
                }
            }
            return points;
        }

        static float Area(List<Vector2> polygon)
        {
            float sum = 0;
            for (int i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i]; var b = polygon[(i + 1) % polygon.Count];
                sum += a.x * b.y - b.x * a.y;
            }
            return sum / 2f;
        }
    }
}
