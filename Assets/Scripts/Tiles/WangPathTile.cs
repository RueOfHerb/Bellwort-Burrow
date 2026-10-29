using UnityEngine;
using UnityEngine.Tilemaps;

namespace BellwortBurrow.Tiles
{
    /// <summary>
    /// A path whose straight runs one tile thick wander a little instead of pinching to the same spot at every join
    /// (art-2.41). It is a Rule Tile like the other ground sets, except on a cell in a straight run one tile thick
    /// (painted on both sides along the run, not across it): there each join between two such cells is given one of
    /// <see cref="States"/> states by a hash of where the join is, so both cells agree on it, and the cell shows the
    /// piece drawn for the states at its two ends (<see cref="across"/>, <see cref="up"/>). State 0 is where every other
    /// path piece meets its neighbours, so a run joins its ends, corners and junctions as before. Setup fills the
    /// pieces in from the sheet's path_across and path_up slices.
    /// </summary>
    [CreateAssetMenu(menuName = "Bellwort Burrow/Wang Path Tile")]
    public class WangPathTile : RuleTile
    {
        /// <summary>How many ways a join can sit: 0 where every path piece meets, 1 and 2 shifted to either side.</summary>
        public const int States = 3;

        [Tooltip("Pieces for a run west to east, one tile thick: index west join's state * 3 + east join's state.")]
        public Sprite[] across = new Sprite[0];

        [Tooltip("Pieces for a run north to south, one tile thick: index north join's state * 3 + south join's state.")]
        public Sprite[] up = new Sprite[0];

        public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
        {
            base.GetTileData(position, tilemap, ref tileData);
            Sprite piece = null;
            if (across.Length == States * States && Straight(tilemap, position, true))
                piece = across[JoinState(tilemap, position + Vector3Int.left, position, true) * States +
                               JoinState(tilemap, position, position + Vector3Int.right, true)];
            else if (up.Length == States * States && Straight(tilemap, position, false))
                piece = up[JoinState(tilemap, position, position + Vector3Int.up, false) * States +
                           JoinState(tilemap, position + Vector3Int.down, position, false)];
            if (piece != null) tileData.sprite = piece;
        }

        /// <summary>A join's state between two neighbouring cells (a west of b, or a south of b): 0 unless both are in a
        /// straight run one tile thick going that way.</summary>
        public int JoinState(ITilemap map, Vector3Int a, Vector3Int b, bool alongX) =>
            Straight(map, a, alongX) && Straight(map, b, alongX) ? StateAt(b, alongX) : 0;

        /// <summary>The state a join takes, from where it is: 0 four times in ten, 1 and 2 three times each.</summary>
        public static int StateAt(Vector3Int join, bool alongX)
        {
            unchecked
            {
                uint h = (uint)join.x * 73856093u ^ (uint)join.y * 19349663u ^ (alongX ? 83492791u : 2654435761u);
                h ^= h >> 13; h *= 0x5bd1e995u; h ^= h >> 15;
                uint r = h % 10u;
                return r < 4 ? 0 : r < 7 ? 1 : 2;
            }
        }

        bool Is(ITilemap map, Vector3Int p) => map.GetTile(p) == this;

        bool Straight(ITilemap map, Vector3Int p, bool alongX)
        {
            if (!Is(map, p)) return false;
            return alongX
                ? Is(map, p + Vector3Int.left) && Is(map, p + Vector3Int.right) && !Is(map, p + Vector3Int.up) && !Is(map, p + Vector3Int.down)
                : Is(map, p + Vector3Int.up) && Is(map, p + Vector3Int.down) && !Is(map, p + Vector3Int.left) && !Is(map, p + Vector3Int.right);
        }

        /// <summary>A cell's piece hangs on the cells one and two away along a run, so refresh those too.</summary>
        public override void RefreshTile(Vector3Int position, ITilemap tilemap)
        {
            base.RefreshTile(position, tilemap);
            foreach (var d in new[] { new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0), new Vector3Int(0, 1, 0), new Vector3Int(0, -1, 0),
                                      new Vector3Int(2, 0, 0), new Vector3Int(-2, 0, 0), new Vector3Int(0, 2, 0), new Vector3Int(0, -2, 0) })
                if (Is(tilemap, position + d)) tilemap.RefreshTile(position + d);
        }
    }
}
