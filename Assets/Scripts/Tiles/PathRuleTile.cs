using UnityEngine;
using UnityEngine.Tilemaps;

namespace BellwortBurrow.Tiles
{
    /// <summary>
    /// A Rule Tile for a path that comes in a full tile and a half tile. It works like Unity's Rule Tile, except its
    /// neighbour conditions can tell the two apart: a half tile fills the half of its cell that touches the full path
    /// beside it, so a tile's piece depends on which neighbours are full path, which are half path and which are neither.
    /// Both tiles of a path are Path Rule Tiles that name each other (<see cref="fullPath"/>, <see cref="halfPath"/>).
    /// </summary>
    [CreateAssetMenu(menuName = "Bellwort Burrow/Path Rule Tile")]
    public class PathRuleTile : RuleTile<PathRuleTile.Neighbor>
    {
        [Tooltip("This path's full tile.")]
        public TileBase fullPath;
        [Tooltip("This path's half tile.")]
        public TileBase halfPath;

        public class Neighbor : RuleTile.TilingRuleOutput.Neighbor
        {
            public const int Full = 3;      // the full path tile
            public const int Half = 4;      // the half path tile
            public const int NoPath = 5;    // neither
            public const int AnyPath = 6;   // full or half
            public const int NotFull = 7;   // half, or no path
            public const int NotHalf = 8;   // full, or no path
        }

        public override bool RuleMatch(int neighbor, TileBase other)
        {
            bool full = other != null && other == fullPath;
            bool half = other != null && other == halfPath;
            switch (neighbor)
            {
                case Neighbor.Full: return full;
                case Neighbor.Half: return half;
                case Neighbor.NoPath: return !full && !half;
                case Neighbor.AnyPath: return full || half;
                case Neighbor.NotFull: return !full;
                case Neighbor.NotHalf: return !half;
            }
            return base.RuleMatch(neighbor, other);
        }
    }
}
