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

        /// <summary>Keeps a camera centre inside <paramref name="limits"/>; on an axis where the limits are smaller than the screen, centres on them.</summary>
        public static Vector2 Clamp(Vector2 want, Rect limits, float halfWidth, float halfHeight)
        {
            want.x = limits.width > 2 * halfWidth ? Mathf.Clamp(want.x, limits.xMin + halfWidth, limits.xMax - halfWidth) : limits.center.x;
            want.y = limits.height > 2 * halfHeight ? Mathf.Clamp(want.y, limits.yMin + halfHeight, limits.yMax - halfHeight) : limits.center.y;
            return want;
        }
    }
}
