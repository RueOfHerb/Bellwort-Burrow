using UnityEngine;
using UnityEngine.Tilemaps;

namespace BellwortBurrow.Tiles
{
    /// <summary>
    /// A ramp up a bank or cliff, climbing its 3 rows: north straight up it, or east or west along it. Just the ramp,
    /// its walkway and its edges, with nothing of the bank around it, laid on a Tilemap of its own over the bank (the
    /// Ramps Tilemap), its bottom 3 rows on the face's rows. Each ramp is one picture of <see cref="columns"/> by
    /// <see cref="rows"/> cells; each cell finds its piece by counting the ramp's cells west of it and above it, so a
    /// block of the right size shows the whole ramp. The face under it stops blocking (<see cref="FaceTile.Covered"/>);
    /// the ramp blocks at its edges and, in its face rows, round its walkway, and the walkway never does. Walking up is
    /// slower and down quicker (<see cref="IInclineTile"/>), and on an east or west ramp walking along it lifts the
    /// walker up the screen (<see cref="IInclineTile.Rise"/>), so it follows the walkway.
    /// A ramp that <see cref="stretches"/> (the North ramps) can be painted any width and length instead: its west and
    /// east edge columns and its back end and face rows stay put, while the middle columns and the deck rows repeat, and
    /// a longer ramp is gentler.
    /// Walking up is slower and down quicker (<see cref="IInclineTile"/>), and on an east or west ramp walking along it
    /// lifts the walker up the screen (<see cref="IInclineTile.Rise"/>), so it follows the walkway up the face.
    /// </summary>
    [CreateAssetMenu(menuName = "Bellwort Burrow/Ramp Tile")]
    public class RampTile : TileBase, IInclineTile
    {
        [Tooltip("The ramp's size in cells.")]
        public int columns = 6, rows = 6;

        [Tooltip("The pieces, row by row from the top, each row west to east. Setup fills these in.")]
        public Sprite[] sprites = new Sprite[36];

        [Tooltip("Which way is up the ramp, on the ground: north (0, 1), east (1, 0) or west (-1, 0).")]
        public Vector2 uphill = Vector2.up;

        [Tooltip("How steep, from 0 (level) to 1 (the steepest: walking straight up is the InclineWalker's uphill speed).")]
        [Range(0f, 1f)] public float steepness = 0.6f;

        [Tooltip("For an east or west ramp: how far up the screen the walkway climbs for each step along it. Zero for a north ramp.")]
        [Range(0f, 1f)] public float lift;

        [Tooltip("How many columns at the low end are a level landing, where the ramp's mouth opens onto the ground.")]
        public int landing;

        [Tooltip("Paint it any width and length: the edge columns, the back end and the face rows stay, the middle columns and deck rows repeat (North ramps).")]
        public bool stretches;

        [Tooltip("How many of its bottom rows are the face it's cut through (the face's height).")]
        public int faceRows = 3;

        bool Here(ITilemap map, Vector3Int p) => map.GetTile(p) == this;
        bool Here(Tilemap map, Vector3Int p) => map.GetTile(p) == this;

        /// <summary>A cell's place in the ramp: its column from the west and its row from the top.</summary>
        public static Vector2Int PlaceOf(System.Func<Vector3Int, bool> isRamp, Vector3Int cell, int columns, int rows)
        {
            int west = 0, above = 0;
            while (isRamp(cell + new Vector3Int(-(west + 1), 0, 0))) west++;
            while (isRamp(cell + new Vector3Int(0, above + 1, 0))) above++;
            return new Vector2Int(west % Mathf.Max(1, columns), above % Mathf.Max(1, rows));
        }

        /// <summary>
        /// A cell's piece in a ramp that stretches: the edge columns at the block's west and east ends and the middle
        /// columns between them in turn; the back end at its top, its bottom <paramref name="faceRows"/> rows the face rows,
        /// and the deck rows between in turn. A block one column wide takes the west edge; one too short for a back end,
        /// deck and face takes what fits from the bottom up.
        /// </summary>
        public static Vector2Int StretchedPlaceOf(System.Func<Vector3Int, bool> isRamp, Vector3Int cell, int columns, int rows, int faceRows)
        {
            int west = Count(isRamp, cell, Vector3Int.left), east = Count(isRamp, cell, Vector3Int.right);
            int above = Count(isRamp, cell, Vector3Int.up), below = Count(isRamp, cell, Vector3Int.down);
            int middles = Mathf.Max(1, columns - 2);
            // the deck rows take turns two at a time, so the stone slabs' staggered rows keep alternating
            int decks = Mathf.Max(1, Mathf.Min(2, rows - faceRows - 1));
            int column = west == 0 || columns < 2 ? 0 : east == 0 ? columns - 1 : 1 + (west - 1) % middles;
            int row = below < faceRows ? rows - 1 - below : above == 0 ? 0 : 1 + (above - 1) % decks;
            return new Vector2Int(column, row);
        }

        static int Count(System.Func<Vector3Int, bool> isRamp, Vector3Int cell, Vector3Int step)
        {
            int n = 0;
            while (n < 256 && isRamp(cell + step * (n + 1))) n++;
            return n;
        }

        Vector2Int Place(System.Func<Vector3Int, bool> isRamp, Vector3Int cell) =>
            stretches ? StretchedPlaceOf(isRamp, cell, columns, rows, faceRows) : PlaceOf(isRamp, cell, columns, rows);

        public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
        {
            var place = Place(p => Here(tilemap, p), position);
            int i = place.y * columns + place.x;
            var sprite = sprites != null && i >= 0 && i < sprites.Length ? sprites[i] : null;
            tileData.sprite = sprite;
            tileData.color = Color.white;
            tileData.transform = Matrix4x4.identity;
            tileData.flags = TileFlags.LockTransform | TileFlags.LockColor;
            tileData.colliderType = sprite != null && sprite.GetPhysicsShapeCount() > 0 ? Tile.ColliderType.Sprite : Tile.ColliderType.None;
        }

        /// <summary>Every cell of a ramp depends on the rest of its block, and the face beside it on it, so refresh the whole block and the cells round it.</summary>
        public override void RefreshTile(Vector3Int position, ITilemap tilemap)
        {
            var seen = new System.Collections.Generic.HashSet<Vector3Int> { position };
            var todo = new System.Collections.Generic.Stack<Vector3Int>();
            todo.Push(position);
            while (todo.Count > 0 && seen.Count < 2048)
            {
                var p = todo.Pop();
                foreach (var d in new[] { Vector3Int.left, Vector3Int.right, Vector3Int.up, Vector3Int.down })
                    if (seen.Add(p + d) && tilemap.GetTile(p + d) == this) todo.Push(p + d);
            }
            foreach (var p in seen) Under.Refresh(tilemap, p);
            foreach (var p in seen)
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        var q = p + new Vector3Int(dx, dy, 0);
                        if (q == position || tilemap.GetTile(q) != null) tilemap.RefreshTile(q);
                    }
        }

        /// <summary>Whether a column of the ramp is its level landing, at the low end.</summary>
        public bool IsLanding(int column) =>
            uphill.x > 0.5f ? column < landing : uphill.x < -0.5f ? column >= columns - landing : false;

        public Vector2 Uphill(Tilemap map, Vector3Int cell)
        {
            if (stretches)
            {
                // rising the face's height over however many rows of deck the block has: longer is gentler
                int length = Count(p => Here(map, p), cell, Vector3Int.up) + Count(p => Here(map, p), cell, Vector3Int.down) + 1;
                float steep = steepness * Mathf.Max(1, rows - faceRows) / Mathf.Max(1, length - faceRows);
                return uphill.normalized * Mathf.Min(1f, steep);
            }
            var place = PlaceOf(p => Here(map, p), cell, columns, rows);
            return IsLanding(place.x) ? Vector2.zero : uphill.normalized * steepness;
        }

        public float Rise(Tilemap map, Vector3Int cell)
        {
            if (Mathf.Abs(uphill.x) < 0.5f || stretches) return 0f;
            var place = PlaceOf(p => Here(map, p), cell, columns, rows);
            return IsLanding(place.x) ? 0f : Mathf.Sign(uphill.x) * lift;
        }
    }
}
