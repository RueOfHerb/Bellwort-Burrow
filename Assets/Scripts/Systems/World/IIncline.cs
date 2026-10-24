using UnityEngine;

namespace BellwortBurrow.Systems.World
{
    /// <summary>
    /// Something other than a tile that a walker can go up or down on, like a bridge's arch. It registers itself with
    /// <see cref="InclineWalker.Register"/> while enabled.
    /// </summary>
    public interface IIncline
    {
        /// <summary>
        /// Whether this carries the walker at its feet right now, and if so which way is uphill there (a direction on
        /// the ground, its length how steep: 1 for 45 degrees). While it carries the walker, the ground under it doesn't count.
        /// </summary>
        bool TryGetUphill(Collider2D walker, Vector2 feet, out Vector2 uphill);
    }
}
