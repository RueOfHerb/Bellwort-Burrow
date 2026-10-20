using System.Collections.Generic;
using UnityEngine;

namespace BellwortBurrow.Systems.World
{
    /// <summary>
    /// A part of a zone with its own camera limits, for zones that aren't one rectangle (an L-shaped path, a room off a hall).
    /// - While the player stands inside it, the camera stays inside its view.
    /// - Stepping into another area pans the camera across to that area's view (see CameraFollow).
    /// - Standing in two areas at once keeps the one the player was already in; standing in none keeps the last one.
    /// A scene without areas keeps the camera on its ground tiles, as before.
    /// </summary>
    public sealed class CameraArea : MonoBehaviour
    {
        [Tooltip("Where the player stands for this area to apply, in world units.")]
        public Rect standing = new Rect(0f, 0f, 30f, 17f);
        [Tooltip("What the camera may show while it applies, in world units. Smaller than the screen centres the camera on it.")]
        public Rect view = new Rect(0f, 0f, 30f, 17f);

        /// <summary>The area for a player at <paramref name="point"/>: the current one while they still stand in it, else the first they stand in, else the current one.</summary>
        public static CameraArea Pick(IReadOnlyList<CameraArea> areas, Vector2 point, CameraArea current)
        {
            if (current != null && current.standing.Contains(point)) return current;
            if (areas != null)
                foreach (var area in areas)
                    if (area != null && area.standing.Contains(point)) return area;
            return current;
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.98f, 0.85f, 0.34f, 0.9f);
            Gizmos.DrawWireCube(standing.center, standing.size);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.62f, 0.8f, 1f);
            Gizmos.DrawWireCube(view.center, view.size);
        }
#endif
    }
}
