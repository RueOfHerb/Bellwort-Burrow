using System;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace BellwortBurrow.Tiles
{
    /// <summary>
    /// Steps cut up through a bank or cliff: stone stairs or log steps. Paint them in place of the face cells they
    /// climb, as many rows tall as the face and one tile wide or more, on the same Tilemap as the face. The face beside
    /// them gets a border where it's cut, and doesn't block where the steps are. Each cell picks its piece from whether
    /// there are steps above, below, west and east of it: the top row where nothing is above, the foot where nothing
    /// is below; the left end, the middle, the right end, or both ends when it's one tile wide.
    /// Walking up the steps is slower and down them quicker (<see cref="IInclineTile"/>): the way up is north.
    /// </summary>
    [CreateAssetMenu(menuName = "Bellwort Burrow/Steps Tile")]
    public class StepsTile : TileBase, IInclineTile
    {
        /// <summary>The pieces' rows and sides: top, middle, bottom; left end, middle, right end, single (one tile wide).</summary>
        public const string PieceRows = "TMB", PieceSides = "lmrs";

        [Tooltip("The pieces, row by row (top, middle, bottom), each row left end, middle, right end, single. Setup fills these in.")]
        public Sprite[] sprites = new Sprite[PieceRows.Length * PieceSides.Length];

        [Tooltip("How steep, from 0 (level) to 1 (the steepest: walking straight up is the InclineWalker's uphill speed).")]
        [Range(0f, 1f)] public float steepness = 1f;

        /// <summary>Which piece a cell takes from whether there are steps above, below, west and east of it.</summary>
        public static (char row, char side) PieceFor(bool n, bool s, bool w, bool e)
        {
            char row = !n ? 'T' : !s ? 'B' : 'M';
            char side = w && e ? 'm' : w ? 'r' : e ? 'l' : 's';
            return (row, side);
        }

        public static int IndexOf(char row, char side) => PieceRows.IndexOf(row) * PieceSides.Length + PieceSides.IndexOf(side);

        bool Here(Func<Vector3Int, TileBase> map, Vector3Int p) => map(p) == this;

        (char row, char side) PieceAt(Func<Vector3Int, TileBase> map, Vector3Int p) =>
            PieceFor(Here(map, p + Vector3Int.up), Here(map, p + Vector3Int.down), Here(map, p + Vector3Int.left), Here(map, p + Vector3Int.right));

        public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
        {
            var (row, side) = PieceAt(p => tilemap.GetTile(p), position);
            int i = IndexOf(row, side);
            tileData.sprite = sprites != null && i < sprites.Length && sprites[i] != null ? sprites[i]
                : sprites != null && sprites.Length > 4 ? sprites[IndexOf('M', 'm')] : null;
            tileData.color = Color.white;
            tileData.transform = Matrix4x4.identity;
            tileData.flags = TileFlags.LockTransform | TileFlags.LockColor;
            tileData.colliderType = Tile.ColliderType.None;
        }

        /// <summary>A cell's piece depends on its neighbours, and the face beside it borders it, so refresh around it.</summary>
        public override void RefreshTile(Vector3Int position, ITilemap tilemap)
        {
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    var p = position + new Vector3Int(dx, dy, 0);
                    if ((dx == 0 && dy == 0) || tilemap.GetTile(p) != null) tilemap.RefreshTile(p);
                }
        }

        public Vector2 Uphill(Tilemap map, Vector3Int cell) => Vector2.up * steepness;
    }
}
