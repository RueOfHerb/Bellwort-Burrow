using UnityEngine;

namespace BellwortBurrow.Systems.World
{
    /// <summary>
    /// Makes a far backdrop drift slower than the ground as the camera moves, so it reads as far away.
    /// - Where you place it is where it sits while the camera is centred on that spot.
    /// - Follow is how much of the camera's movement it keeps up with: 0 stays put like the ground, 1 moves with the
    ///   camera like the sky. Across and up and down are set apart; most backdrops only drift across.
    /// - It moves after the camera has moved for the frame, snapped to whole pixels like the camera, so it never shimmers.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class Parallax : MonoBehaviour
    {
        [Tooltip("How much of the camera's movement it keeps up with, across and up and down. 0 is the ground, 1 is the sky.")]
        [SerializeField] Vector2 follow = new Vector2(0.5f, 0f);
        [SerializeField] int pixelsPerUnit = 16;
        [Tooltip("The camera to follow. Leave empty for the main camera.")]
        [SerializeField] Camera view;

        Vector3 placed;
        bool started;

        public Vector2 Follow { get => follow; set => follow = value; }

        void Awake()
        {
            placed = transform.position;
            started = true;
        }

        void LateUpdate()
        {
            if (!started) Awake();
            if (view == null) view = Camera.main;
            if (view == null) return;
            transform.position = Drifted(placed, view.transform.position, follow, pixelsPerUnit);
        }

        /// <summary>
        /// Where the backdrop goes for this camera position: its placed spot, moved by the share of the camera's
        /// distance from that spot it follows, snapped to whole pixels (none when pixelsPerUnit is 0).
        /// </summary>
        public static Vector3 Drifted(Vector3 placed, Vector3 camera, Vector2 follow, int pixelsPerUnit)
        {
            float x = placed.x + (camera.x - placed.x) * follow.x;
            float y = placed.y + (camera.y - placed.y) * follow.y;
            if (pixelsPerUnit > 0)
            {
                x = Mathf.Round(x * pixelsPerUnit) / pixelsPerUnit;
                y = Mathf.Round(y * pixelsPerUnit) / pixelsPerUnit;
            }
            return new Vector3(x, y, placed.z);
        }
    }
}
