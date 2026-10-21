using UnityEngine;
using UnityEngine.Tilemaps;

namespace BellwortBurrow.Tiles
{
    /// <summary>
    /// The face of raised ground, a bank's earth or a cliff's stone, seen from the front. Paint it in the rows under a
    /// Bank Top's front edge, as many rows tall as the bank should be. Each cell picks its piece by which of its four
    /// sides have more of the same face (so its top row hangs the lip's grass, its bottom row is the foot and its ends
    /// turn back) and by where it sits on the map: column x mod <see cref="Columns"/> and row y mod <see cref="Rows"/>
    /// (counted down), its phase. The face's texture is bigger than a tile, so a lump of earth or a stone can run on
    /// across a seam, and a long face doesn't repeat every tile.
    /// The pieces are made by setup from a face set on a ground sheet (see GroundAutoTiles.FaceTemplate).
    /// </summary>
    [CreateAssetMenu(menuName = "Bellwort Burrow/Face Tile")]
    public class FaceTile : TileBase
    {
        /// <summary>How many columns and rows of tiles the face's texture spans before it repeats.</summary>
        public const int Columns = 3, Rows = 2;

        /// <summary>A side where the face carries on: more of this tile there.</summary>
        public const int North = 1, South = 2, West = 4, East = 8;

        /// <summary>How many layouts a face has: every mix of the four sides.</summary>
        public const int Layouts = 16;

        [Tooltip("Every piece, by layout and phase (see Index). Setup fills this in.")]
        public Sprite[] pieces = new Sprite[Layouts * Columns * Rows];

        [Tooltip("Shown where a piece is missing.")]
        public Sprite defaultSprite;

        /// <summary>Where a layout's piece at a phase sits in <see cref="pieces"/>.</summary>
        public static int Index(int layout, int column, int row) => (layout * Rows + row) * Columns + column;

        /// <summary>A cell's phase: its column x mod 3 and its row mod 2, rows counted down the map as on the sheet.</summary>
        public static (int column, int row) Phase(Vector3Int position) => (Mod(position.x, Columns), Mod(-position.y, Rows));

        static int Mod(int a, int b) => ((a % b) + b) % b;

        static readonly (Vector3Int offset, int side)[] Around =
        {
            (Vector3Int.up, North), (Vector3Int.down, South), (Vector3Int.left, West), (Vector3Int.right, East),
        };

        /// <summary>Which sides of a cell have more of this face.</summary>
        public int LayoutAt(ITilemap tilemap, Vector3Int position)
        {
            int layout = 0;
            foreach (var (offset, side) in Around)
                if (tilemap.GetTile(position + offset) == this) layout |= side;
            return layout;
        }

        /// <summary>The piece for a layout at a phase, or the nearest stand-in: the same layout at another phase, then the default.</summary>
        public Sprite PieceFor(int layout, int column, int row)
        {
            if (pieces == null || pieces.Length < Layouts * Columns * Rows) return defaultSprite;
            var piece = pieces[Index(layout, column, row)];
            if (piece != null) return piece;
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Columns; c++)
                    if (pieces[Index(layout, c, r)] != null) return pieces[Index(layout, c, r)];
            return defaultSprite;
        }

        public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
        {
            var (column, row) = Phase(position);
            tileData.sprite = PieceFor(LayoutAt(tilemap, position), column, row);
            tileData.color = Color.white;
            tileData.transform = Matrix4x4.identity;
            tileData.flags = TileFlags.LockTransform | TileFlags.LockColor;
            tileData.colliderType = Tile.ColliderType.None;
        }

        /// <summary>A cell's piece depends on its four sides, so painting or erasing one refreshes the face around it.</summary>
        public override void RefreshTile(Vector3Int position, ITilemap tilemap)
        {
            tilemap.RefreshTile(position);
            foreach (var (offset, _) in Around)
                if (tilemap.GetTile(position + offset) == this) tilemap.RefreshTile(position + offset);
        }
    }
}
