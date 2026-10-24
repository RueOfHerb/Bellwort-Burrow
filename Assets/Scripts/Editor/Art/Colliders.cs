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
        /// The footprint of a slice, in pixels from its feet (its pivot, or the middle of its bottom edge), x to the
        /// right and y up, going round once. Null when the slice has no solid pixels.
        /// </summary>
        public static List<Vector2> Footprint(bool[] solid, int sheetWidth, int sheetHeight, SheetSlice slice)
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

            float width = b - a + 1;
            float middle = Math.Min(feet - width * FootprintDepth / 2f, highest);

            var edge = new List<Vector2>();
            for (int i = a; i <= b; i++)
            {
                if (i != a && i != b && (i - a) % Step != 0) continue;
                float px = left + (i == a ? i : i == b ? i + 1 : i + 0.5f);
                edge.Add(new Vector2(px, front[i]));
            }
            if (a == b) edge.Add(new Vector2(left + a + 1, front[a]));
            edge = Simplify(edge, 1f);

            var shape = new List<Vector2>(edge.Count * 2);
            foreach (var p in edge) shape.Add(new Vector2(p.x - pivotX, feet - p.y));
            for (int i = edge.Count - 1; i >= 0; i--)
            {
                float back = 2 * middle - edge[i].y;
                shape.Add(new Vector2(edge[i].x - pivotX, feet - back));
            }
            return shape;
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
        /// Ground tiles that block the player: the water, full and half (Burrow Water, Burrow Water Half). Its tiles
        /// carry a physics shape made of quarters (<see cref="QuarterShape"/>), so a half tile blocks only the half it
        /// fills and a slope cuts at 45 degrees.
        /// </summary>
        public static bool IsBlockingGroundSlice(string sliceName) =>
            !string.IsNullOrEmpty(sliceName) && sliceName.StartsWith("water_", StringComparison.Ordinal);

        /// <summary>A raised ground's face set (bank_face_set, cliff_face_set and the rest of GroundAutoTiles.Faces), whose tiles block by <see cref="FaceShape"/>.</summary>
        public static bool IsFaceSlice(string sliceName) =>
            Array.Exists(GroundAutoTiles.Faces, f => f.slice == sliceName);

        // ---- Tile shapes: every edge level, upright or at 45 degrees -------------------------------------------------

        /// <summary>
        /// A ground tile's physics shape from its picture, made of its four quarters (8 px squares for a 16 px tile), each
        /// whole, empty, or cut corner to corner into a triangle, the way the ground sets are drawn (half tiles fill
        /// quarters; slopes cut a quarter or a tile corner to corner). So the shapes only have level, upright and
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
    }
}
