using UnityEngine;
using UnityEngine.Tilemaps;

namespace BellwortBurrow.Tiles
{
    /// <summary>
    /// A tile you walk up or down: slopes, ramps and steps. Walking on one is slower uphill and quicker downhill
    /// (InclineWalker, in Systems, reads it under the walker's feet).
    /// </summary>
    public interface IInclineTile
    {
        /// <summary>
        /// Which way is uphill at a cell this tile is painted in, as a direction on the ground (north is up the screen).
        /// Its length is how steep it is: 1 for the steepest kinds (steep slopes and steps), less for gentler ones, and
        /// zero where the cell is level.
        /// </summary>
        Vector2 Uphill(Tilemap map, Vector3Int cell);
    }
}
