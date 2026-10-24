using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace BellwortBurrow.Tiles
{
    /// <summary>
    /// The face of raised ground, a bank's earth or a cliff's stone, seen from the front. Paint it under the raised
    /// grass's front edge, three rows tall. Its top row draws the lip, the grass rounding over the edge with a fringe hanging
    /// from it; its bottom row is the foot.
    /// Each cell works out its piece by looking up and down its own column, and at its neighbours' columns:
    /// - Where the face beside it starts a row higher or lower (a step), the block further south is in front, and its
    ///   end at the step gets a border.
    /// - A column painted four rows tall, between columns whose tops step one row at a time, is a 45 degree run: the
    ///   lip crosses its top cell from corner to corner and the foot its bottom cell. Paint a staircase of these to
    ///   turn a bank 45 degrees.
    /// - Where a 45 degree run meets a flat run in an inner corner, the flat side gets a crease.
    /// - An end against open ground is jagged; an end against steps cut into it (<see cref="StepsTile"/>) gets a border.
    /// Every look has several variations, picked by where the cell sits, so a long face never repeats.
    /// The pieces are made by setup from a face set on a ground sheet (see GroundAutoTiles.FacePieceKeys).
    /// </summary>
    [CreateAssetMenu(menuName = "Bellwort Burrow/Face Tile")]
    public class FaceTile : TileBase
    {
        /// <summary>How many rows a flat face is painted by default; a 45 degree column is one more.</summary>
        public const int Rows = 3;

        [Tooltip("How many rows a flat run is painted (a 45 degree column is one more): 3 for a bank or cliff, 2 for a steep slope.")]
        [Min(1)] public int rows = Rows;

        [Tooltip("Pieces pick their look by column, the same all the way down it, and a flat run carrying on both ways " +
                 "takes its looks in order along the face (column x mod how many it has), so its looks can be drawn as one " +
                 "strip that crosses the seams (the root bank's roots).")]
        public bool stripLooks;

        [Tooltip("Faces of the same family join up as one face where they meet (Bank Face and Root Bank Face, Cliff Face " +
                 "and Mossy Cliff Face), so you can paint one into the other. Empty: joins only itself.")]
        public string family = "";

        /// <summary>
        /// The key of each sprite in <see cref="sprites"/>: shape (F flat, D 45 degrees down to the east, U up), row (T
        /// top, M middle, B bottom, S a face one row tall; for D and U the cell's place down its column, 0 to 3), then
        /// the west and east sides (c carries on, o open end, b step border, k inner corner crease). A key that comes
        /// up more than once has variations. Setup fills these in.
        /// </summary>
        public string[] keys = new string[0];
        public Sprite[] sprites = new Sprite[0];

        [Tooltip("Shown where a piece is missing.")]
        public Sprite defaultSprite;

        [Tooltip("The player can't walk through the face: each cell collides by its piece's shape, a whole tile on a flat " +
                 "face and a 45 degree cut at the lip and foot of a 45 degree run. A stair or ramp up the bank, painted in place " +
                 "of the face cells it climbs, lets them through there.")]
        public bool solid = true;

        Dictionary<string, List<Sprite>> byKey;

        /// <summary>A run of this face down one column: its top and bottom cells' y (y up, so top >= bottom).</summary>
        public readonly struct Run
        {
            public readonly int top, bottom;
            readonly int rows;
            public Run(int top, int bottom, int rows = Rows) { this.top = top; this.bottom = bottom; this.rows = rows; }
            public int Height => top - bottom + 1;
            public bool Diagonal => Height == rows + 1;
        }

        bool IsFace(Func<Vector3Int, TileBase> map, int x, int y) => Joins(map(new Vector3Int(x, y, 0)));

        /// <summary>Whether a tile carries this face on: this tile, or a face of the same family and height.</summary>
        public bool Joins(TileBase tile) =>
            tile == this || (tile is FaceTile other && !(other is SlopeTile) && !(this is SlopeTile) &&
                             !string.IsNullOrEmpty(family) && other.family == family && other.rows == rows);

        static Func<Vector3Int, TileBase> Reader(ITilemap map) => p => map.GetTile(p);

        /// <summary>The run of face through (x, y), or null if that cell isn't this face.</summary>
        public Run? RunAt(ITilemap map, int x, int y) => RunAt(Reader(map), x, y);

        public Run? RunAt(Func<Vector3Int, TileBase> map, int x, int y)
        {
            if (!IsFace(map, x, y)) return null;
            int top = y, bottom = y;
            while (IsFace(map, x, top + 1)) top++;
            while (IsFace(map, x, bottom - 1)) bottom--;
            return new Run(top, bottom, rows);
        }

        /// <summary>The run in column x beside a run: the first face cell from a row above its top to a row below its foot.</summary>
        public Run? NeighbourRun(ITilemap map, int x, Run run) => NeighbourRun(Reader(map), x, run);

        public Run? NeighbourRun(Func<Vector3Int, TileBase> map, int x, Run run)
        {
            for (int y = run.top + 1; y >= run.bottom - 1; y--)
                if (IsFace(map, x, y)) return RunAt(map, x, y);
            return null;
        }

        /// <summary>
        /// A cell's piece key (without its variation), and the cell its variation is picked by: its own, or for a 45
        /// degree column, the column's top, so the four cells of the column agree.
        /// </summary>
        public (string key, Vector2Int cell) KeyAt(ITilemap map, Vector3Int position) => KeyAt(Reader(map), position);

        /// <summary>The same, reading the cells through <paramref name="map"/> (a Tilemap's GetTile at run time).</summary>
        public (string key, Vector2Int cell) KeyAt(Func<Vector3Int, TileBase> map, Vector3Int position)
        {
            int x = position.x, y = position.y;
            var run = RunAt(map, x, y);
            if (run == null) return (null, default);
            var r = run.Value;
            var west = NeighbourRun(map, x - 1, r);
            var east = NeighbourRun(map, x + 1, r);
            if (r.Diagonal)
            {
                int down = 0, up = 0;
                if (west is Run w)
                {
                    if ((w.Diagonal && w.top == r.top + 1) || (!w.Diagonal && w.top == r.top)) down++;
                    if (w.top == r.top - 1) up++;
                }
                if (east is Run e)
                {
                    if (e.top == r.top - 1) down++;
                    if ((e.Diagonal && e.top == r.top + 1) || (!e.Diagonal && e.top == r.top)) up++;
                }
                char shape = up > down ? 'U' : 'D';
                char ws = west == null ? 'o' : 'c', es = east == null ? 'o' : 'c';
                if (ws == 'o' && es == 'o') ws = 'c';
                return ($"{shape}{(char)('0' + (r.top - y))}{ws}{es}", new Vector2Int(x, r.top));
            }
            bool top = y == r.top, bottom = y == r.bottom;
            char row = top && bottom ? 'S' : top ? 'T' : bottom ? 'B' : 'M';
            char westSide = west == null && map(new Vector3Int(x - 1, y, 0)) is StepsTile ? 'b' : Side(west, r);
            char eastSide = east == null && map(new Vector3Int(x + 1, y, 0)) is StepsTile ? 'b' : Side(east, r);
            return ($"F{row}{westSide}{eastSide}", new Vector2Int(x, stripLooks ? r.top : y));
        }

        // A flat run's side: open against grass; a crease where a 45 degree run beside it starts a row higher (the
        // inner corner at the foot of a run down, or the start of a run up); a border where the flat run beside it
        // starts higher, so this block is the one in front; otherwise it carries on.
        static char Side(Run? beside, Run run)
        {
            if (beside == null) return 'o';
            var b = beside.Value;
            if (b.Diagonal) return b.top == run.top + 1 ? 'k' : 'c';
            return b.top > run.top ? 'b' : 'c';
        }

        void Index()
        {
            byKey = new Dictionary<string, List<Sprite>>();
            if (keys == null || sprites == null) return;
            for (int i = 0; i < keys.Length && i < sprites.Length; i++)
            {
                if (sprites[i] == null || string.IsNullOrEmpty(keys[i])) continue;
                if (!byKey.TryGetValue(keys[i], out var list)) byKey[keys[i]] = list = new List<Sprite>();
                list.Add(sprites[i]);
            }
        }

        void OnValidate() => byKey = null;
        void OnEnable() => byKey = null;

        /// <summary>The piece for a key at a cell: one of its variations, or the nearest stand-in if it has none.</summary>
        public Sprite PieceFor(string key, Vector2Int cell)
        {
            if (byKey == null) Index();
            if (key == null) return defaultSprite;
            foreach (var k in StandIns(key))
                if (byKey.TryGetValue(k, out var list) && list.Count > 0)
                    return list[stripLooks && k[0] == 'F' && k[2] == 'c' && k[3] == 'c' && k[1] != 'S'
                        ? ((cell.x % list.Count) + list.Count) % list.Count
                        : Pick(cell, k[0], list.Count)];
            return defaultSprite;
        }

        // The key itself, then with its sides plainer, then the plainest piece of its row.
        static IEnumerable<string> StandIns(string key)
        {
            yield return key;
            char s = key[0], r = key[1], w = key[2], e = key[3];
            if (w != 'c') yield return $"{s}{r}c{e}";
            if (e != 'c') yield return $"{s}{r}{w}c";
            yield return $"{s}{r}cc";
            if (s != 'F') yield return "FMcc";
        }

        /// <summary>A cell's variation of a piece: the same every time, and scattered so a face doesn't repeat.</summary>
        public static int Pick(Vector2Int cell, char shape, int count)
        {
            unchecked
            {
                uint h = (uint)cell.x * 73856093u ^ (uint)cell.y * 19349663u ^ shape * 83492791u;
                h ^= h >> 13; h *= 0x5bd1e995u; h ^= h >> 15;
                return (int)(h % (uint)count);
            }
        }

        public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
        {
            var (key, cell) = KeyAt(tilemap, position);
            tileData.sprite = PieceFor(key, cell);
            tileData.color = Color.white;
            tileData.transform = Matrix4x4.identity;
            tileData.flags = TileFlags.LockTransform | TileFlags.LockColor;
            tileData.colliderType = solid ? Tile.ColliderType.Sprite : Tile.ColliderType.None;
        }

        /// <summary>
        /// A cell's piece depends on its column and its neighbours', so painting one refreshes everything around it.
        /// </summary>
        public override void RefreshTile(Vector3Int position, ITilemap tilemap)
        {
            for (int dx = -2; dx <= 2; dx++)
                for (int dy = -rows - 2; dy <= rows + 2; dy++)
                {
                    var p = position + new Vector3Int(dx, dy, 0);
                    if ((dx == 0 && dy == 0) || tilemap.GetTile(p) != null) tilemap.RefreshTile(p);
                }
        }
    }
}
