using UnityEngine;
using UnityEngine.Tilemaps;

namespace BellwortBurrow.Tiles
{
    /// <summary>
    /// Grass you walk up: raised ground's front as a slope instead of a wall. It's painted like a face (see
    /// <see cref="FaceTile"/>): under the raised grass's front edge, <see cref="FaceTile.rows"/> rows tall (one more in a
    /// 45 degree column), and each cell works out its piece from its own column and its neighbours'. A slope facing you
    /// (rising north) is a step darker than flat ground, a 45 degree run rising to the east faces the light and is
    /// lighter, one rising to the west is darkest; a lit crest at the top, a crease at the foot.
    /// Nothing blocks on a slope. Walking up it is slower and down it quicker (<see cref="IInclineTile"/>): the way up is
    /// north on a flat run, north-west on a run going down to the east, north-east on one going up to the east.
    /// The pieces are made by setup from a slope set laid out like a face set (GroundAutoTiles.Slopes).
    /// </summary>
    [CreateAssetMenu(menuName = "Bellwort Burrow/Slope Tile")]
    public class SlopeTile : FaceTile, IInclineTile
    {
        [Tooltip("How steep, from 0 (level) to 1 (the steepest: walking straight up it is the InclineWalker's uphill speed).")]
        [Range(0f, 1f)] public float steepness = 1f;

        public SlopeTile() { solid = false; }

        static readonly Vector2 NorthWest = new Vector2(-1f, 1f).normalized, NorthEast = new Vector2(1f, 1f).normalized;

        /// <summary>Which way is uphill for a piece key: its length is <see cref="steepness"/>.</summary>
        public Vector2 UphillFor(string key)
        {
            if (string.IsNullOrEmpty(key)) return Vector2.zero;
            Vector2 way = key[0] == 'D' ? NorthWest : key[0] == 'U' ? NorthEast : Vector2.up;
            return way * steepness;
        }

        public Vector2 Uphill(Tilemap map, Vector3Int cell) => UphillFor(KeyAt(p => map.GetTile(p), cell).key);
    }
}
