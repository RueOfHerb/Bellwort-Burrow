using System.Collections.Generic;
using BellwortBurrow.Systems.World;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace BellwortBurrow.Player
{
    /// <summary>
    /// Keeps the camera on the player, snapped to whole pixels, and inside the zone's ground tiles
    /// so the view never runs off the edge of the map.
    /// A zone that isn't one rectangle has <see cref="CameraArea"/>s instead: the camera keeps to the view of the area
    /// the player stands in, and pans across when they step into another.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] Transform target;
        [Tooltip("Where the camera centres, relative to the target's feet (in units).")]
        [SerializeField] Vector2 offset = new Vector2(0f, 1f);
        [Tooltip("The zone's Grid; the camera stays inside the area its tilemaps cover. Leave empty for no limit.")]
        [SerializeField] Grid bounds;
        [SerializeField] int pixelsPerUnit = 16;
        [Tooltip("How long the camera takes to pan across when the player steps into another camera area (in seconds).")]
        [SerializeField] float panTime = 0.6f;

        Camera view;
        Bounds area;
        bool limited;
        CameraArea[] areas;
        CameraArea current;
        Vector2 panOffset, panVelocity;
        bool placed;

        void Awake()
        {
            view = GetComponent<Camera>();
            areas = FindObjectsByType<CameraArea>(FindObjectsInactive.Exclude);
            if (bounds == null) return;
            foreach (var map in bounds.GetComponentsInChildren<Tilemap>())
            {
                map.CompressBounds();
                if (map.cellBounds.size.x == 0 || map.cellBounds.size.y == 0) continue;
                var world = new Bounds(map.transform.TransformPoint(map.localBounds.center), map.localBounds.size);
                if (limited) area.Encapsulate(world); else { area = world; limited = true; }
            }
        }

        void LateUpdate()
        {
            if (target == null) return;
            Vector2 want = (Vector2)target.position + offset;
            float halfHeight = view.orthographicSize, halfWidth = halfHeight * view.aspect;
            Vector2 was = transform.position;

            var next = CameraArea.Pick(areas, target.position, current);
            bool switched = next != current;
            current = next;
            if (current != null) want = Clamp(want, current.view, halfWidth, halfHeight);
            else if (limited) want = Clamp(want, new Rect(area.min, area.size), halfWidth, halfHeight);

            // Into another area: start from where the camera was and ease the difference away.
            if (switched && placed) { panOffset = was - want; panVelocity = Vector2.zero; }
            panOffset = panTime > 0f ? Vector2.SmoothDamp(panOffset, Vector2.zero, ref panVelocity, panTime) : Vector2.zero;
            if (panOffset.sqrMagnitude < 1e-6f) panOffset = Vector2.zero;
            want += panOffset;
            placed = true;

            want.x = Mathf.Round(want.x * pixelsPerUnit) / pixelsPerUnit;
            want.y = Mathf.Round(want.y * pixelsPerUnit) / pixelsPerUnit;
            transform.position = new Vector3(want.x, want.y, transform.position.z);
        }

        /// <summary>The target the camera follows.</summary>
        public Transform Target => target;
        /// <summary>Where the camera centres, relative to the target's feet (in units).</summary>
        public Vector2 Offset => offset;
        /// <summary>The zone's Grid the camera stays inside when there are no camera areas, or null for no limit.</summary>
        public Grid Limits => bounds;

        /// <summary>
        /// Where the camera settles for a player standing still at <paramref name="feet"/>: <paramref name="offset"/> above
        /// them, kept inside the view of the camera area they stand in, or inside <paramref name="ground"/> when they stand in
        /// none, snapped to whole pixels. Canvas snapshots use it to frame the screen the player sees there.
        /// </summary>
        public static Vector2 SettledView(Vector2 feet, Vector2 offset, IReadOnlyList<CameraArea> areas, Rect? ground,
            float halfWidth, float halfHeight, int pixelsPerUnit)
        {
            Vector2 want = feet + offset;
            var area = CameraArea.Pick(areas, feet, null);
            if (area != null) want = Clamp(want, area.view, halfWidth, halfHeight);
            else if (ground.HasValue) want = Clamp(want, ground.Value, halfWidth, halfHeight);
            if (pixelsPerUnit > 0)
            {
                want.x = Mathf.Round(want.x * pixelsPerUnit) / pixelsPerUnit;
                want.y = Mathf.Round(want.y * pixelsPerUnit) / pixelsPerUnit;
            }
            return want;
        }

        /// <summary>The ground a Grid's tilemaps cover, in world units, or null when it has nothing painted.</summary>
        public static Rect? GroundOf(Grid grid)
        {
            if (grid == null) return null;
            bool any = false;
            Vector3 min = Vector3.zero, max = Vector3.zero;
            foreach (var map in grid.GetComponentsInChildren<Tilemap>())
            {
                var cells = map.cellBounds;
                if (cells.size.x <= 0 || cells.size.y <= 0) continue;
                Vector3 a = map.CellToWorld(cells.min), b = map.CellToWorld(cells.max);
                if (!any) { min = Vector3.Min(a, b); max = Vector3.Max(a, b); any = true; }
                else { min = Vector3.Min(min, Vector3.Min(a, b)); max = Vector3.Max(max, Vector3.Max(a, b)); }
            }
            return any ? Rect.MinMaxRect(min.x, min.y, max.x, max.y) : (Rect?)null;
        }

        /// <summary>Keeps a camera centre inside <paramref name="limits"/>; on an axis where the limits are smaller than the screen, centres on them.</summary>
        public static Vector2 Clamp(Vector2 want, Rect limits, float halfWidth, float halfHeight)
        {
            want.x = limits.width > 2 * halfWidth ? Mathf.Clamp(want.x, limits.xMin + halfWidth, limits.xMax - halfWidth) : limits.center.x;
            want.y = limits.height > 2 * halfHeight ? Mathf.Clamp(want.y, limits.yMin + halfHeight, limits.yMax - halfHeight) : limits.center.y;
            return want;
        }
    }
}
