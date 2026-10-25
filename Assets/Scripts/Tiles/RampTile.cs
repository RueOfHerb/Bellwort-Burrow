using UnityEngine;
using UnityEngine.Tilemaps;

namespace BellwortBurrow.Tiles
{
    /// <summary>
    /// A ramp cut into a bank or cliff, climbing its 3 rows: north straight into the hill, or east or west along the
    /// face. Each ramp is one picture of <see cref="columns"/> by <see cref="rows"/> cells (the face around the cut is
    /// drawn into it). Paint it as that block, in place of the face cells it covers and the hill top cells behind them,
    /// on the same Tilemap as the face (pick the whole block from the tile palette and stamp it). Each cell finds its
    /// piece by counting the ramp's cells west of it and above it, so a block of the right size shows the whole ramp.
    /// The face beside it carries on into it. Only the walls, the face and the edges block; the walkway never does.
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

        public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
        {
            var place = PlaceOf(p => Here(tilemap, p), position, columns, rows);
            int i = place.y * columns + place.x;
            var sprite = sprites != null && i >= 0 && i < sprites.Length ? sprites[i] : null;
            tileData.sprite = sprite;
            tileData.color = Color.white;
            tileData.transform = Matrix4x4.identity;
            tileData.flags = TileFlags.LockTransform | TileFlags.LockColor;
            tileData.colliderType = sprite != null && sprite.GetPhysicsShapeCount() > 0 ? Tile.ColliderType.Sprite : Tile.ColliderType.None;
        }

        /// <summary>Every cell of a ramp depends on the cells west of and above it, and the face beside it on it, so refresh the block around it.</summary>
        public override void RefreshTile(Vector3Int position, ITilemap tilemap)
        {
            for (int dx = -1; dx <= columns; dx++)
                for (int dy = -rows; dy <= 1; dy++)
                {
                    var p = position + new Vector3Int(dx, dy, 0);
                    if ((dx == 0 && dy == 0) || tilemap.GetTile(p) != null) tilemap.RefreshTile(p);
                }
        }

        /// <summary>Whether a column of the ramp is its level landing, at the low end.</summary>
        public bool IsLanding(int column) =>
            uphill.x > 0.5f ? column < landing : uphill.x < -0.5f ? column >= columns - landing : false;

        public Vector2 Uphill(Tilemap map, Vector3Int cell)
        {
            var place = PlaceOf(p => Here(map, p), cell, columns, rows);
            return IsLanding(place.x) ? Vector2.zero : uphill.normalized * steepness;
        }

        public float Rise(Tilemap map, Vector3Int cell)
        {
            if (Mathf.Abs(uphill.x) < 0.5f) return 0f;
            var place = PlaceOf(p => Here(map, p), cell, columns, rows);
            return IsLanding(place.x) ? 0f : Mathf.Sign(uphill.x) * lift;
        }
    }
}
