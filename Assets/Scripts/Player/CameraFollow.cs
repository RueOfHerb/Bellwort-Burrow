using UnityEngine;
using UnityEngine.Tilemaps;

namespace BellwortBurrow.Player
{
    /// <summary>
    /// Keeps the camera on the player, snapped to whole pixels, and inside the zone's ground tiles
    /// so the view never runs off the edge of the map.
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

        Camera view;
        Bounds area;
        bool limited;

        void Awake()
        {
            view = GetComponent<Camera>();
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
            if (limited)
            {
                float halfHeight = view.orthographicSize, halfWidth = halfHeight * view.aspect;
                want.x = area.size.x > 2 * halfWidth ? Mathf.Clamp(want.x, area.min.x + halfWidth, area.max.x - halfWidth) : area.center.x;
                want.y = area.size.y > 2 * halfHeight ? Mathf.Clamp(want.y, area.min.y + halfHeight, area.max.y - halfHeight) : area.center.y;
            }
            want.x = Mathf.Round(want.x * pixelsPerUnit) / pixelsPerUnit;
            want.y = Mathf.Round(want.y * pixelsPerUnit) / pixelsPerUnit;
            transform.position = new Vector3(want.x, want.y, transform.position.z);
        }
    }
}
