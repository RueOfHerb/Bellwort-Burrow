using BellwortBurrow.Systems.World;
using UnityEngine;

namespace BellwortBurrow.Player
{
    /// <summary>
    /// A spot for a canvas snapshot. Capture Shots stands the player here and saves the 480 by 270 screen the camera settles
    /// on, as in play: kept inside the camera area the spot is in (or the ground), with every backdrop drifted for that camera.
    /// - The object's name names the image, and the order of the spots in the Hierarchy is their order on the canvas.
    /// - Keep the spots under one object tagged EditorOnly (Canvas snapshots, in each zone scene), so builds leave them out.
    /// - In the Scene view each spot shows the screen it saves, and a small diamond where the player's feet go.
    /// Nothing happens in the game.
    /// </summary>
    [AddComponentMenu("Bellwort Burrow/Snapshot Spot")]
    public sealed class SnapshotSpot : MonoBehaviour
    {
        public const int ScreenWidth = 480, ScreenHeight = 270, PixelsPerUnit = 16;

        /// <summary>The middle of the screen the camera settles on for a player standing on this spot.</summary>
        public Vector2 View(CameraFollow follow, CameraArea[] areas)
        {
            Vector2 offset = follow != null ? follow.Offset : new Vector2(0f, 1f);
            Rect? ground = follow != null ? CameraFollow.GroundOf(follow.Limits) : null;
            return CameraFollow.SettledView(transform.position, offset, areas, ground,
                ScreenWidth / 2f / PixelsPerUnit, ScreenHeight / 2f / PixelsPerUnit, PixelsPerUnit);
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            var follow = FindAnyObjectByType<CameraFollow>();
            var areas = FindObjectsByType<CameraArea>(FindObjectsInactive.Exclude);
            Gizmos.color = new Color(0.83f, 0.59f, 0.63f, 0.9f);
            Gizmos.DrawWireCube(View(follow, areas), new Vector3(ScreenWidth, ScreenHeight) / PixelsPerUnit);
            Vector3 feet = transform.position;
            const float r = 0.5f;
            Gizmos.DrawLine(feet + Vector3.left * r, feet + Vector3.up * r);
            Gizmos.DrawLine(feet + Vector3.up * r, feet + Vector3.right * r);
            Gizmos.DrawLine(feet + Vector3.right * r, feet + Vector3.down * r);
            Gizmos.DrawLine(feet + Vector3.down * r, feet + Vector3.left * r);
        }
#endif
    }
}
