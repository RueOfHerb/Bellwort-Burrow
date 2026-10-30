using UnityEngine;

namespace BellwortBurrow.Player
{
    /// <summary>
    /// Marks what the Interact action would use right now (ui-5): four corner brackets round the farm tile you'd till,
    /// plant, water or harvest, or round the forageable or door you'd use. It asks <see cref="PlayerInteractor.TryGetTarget"/>
    /// every frame, the same choice Interact makes, so it shows only where pressing Interact does something. The brackets
    /// are drawn here in Bellwort flower Light, one pixel thick and four long, snapped to the pixel grid, and sort under
    /// the characters and above the ground. PlayerInteractor makes one under the Player when it has none.
    /// </summary>
    public class TargetIndicator : MonoBehaviour
    {
        [SerializeField] PlayerInteractor interactor;
        [Tooltip("The brackets' colour: Bellwort flower Light from the Burrow palette.")]
        [SerializeField] Color color = new Color32(241, 217, 136, 255);
        [Tooltip("How long each arm of a bracket is, in pixels.")]
        [SerializeField, Range(2, 8)] int arm = 4;
        [Tooltip("Pixels between a forageable's or door's picture and the brackets round it (a farm tile gets none).")]
        [SerializeField, Range(0, 4)] int padding = 1;
        [Tooltip("Above the ground tilemaps (-30 to -10), under everything that stands in the world (0 and up).")]
        [SerializeField] int sortingOrder = -5;

        const float PixelsPerUnit = 16f;
        readonly SpriteRenderer[] corners = new SpriteRenderer[4];
        Sprite bracket;

        /// <summary>Which player's target to mark.</summary>
        public void Follow(PlayerInteractor who) => interactor = who;

        void Awake()
        {
            if (interactor == null) interactor = GetComponentInParent<PlayerInteractor>();
            bracket = MakeBracket(arm);
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject($"Corner {i}");
                go.transform.SetParent(transform, false);
                var sprite = go.AddComponent<SpriteRenderer>();
                sprite.sprite = bracket;
                sprite.color = color;
                sprite.sortingOrder = sortingOrder;
                sprite.flipX = i == 1 || i == 3;   // 0 bottom left, 1 bottom right, 2 top left, 3 top right
                sprite.flipY = i >= 2;
                corners[i] = sprite;
            }
            Show(false);
        }

        void OnDestroy()
        {
            if (bracket != null) { Destroy(bracket.texture); Destroy(bracket); }
        }

        void LateUpdate()
        {
            if (interactor == null || !interactor.isActiveAndEnabled || !interactor.TryGetTarget(out var frame))
            {
                Show(false);
                return;
            }
            var (min, max) = Snap(frame, interactor.IsFarmTarget ? 0 : padding);
            Place(0, new Vector2(min.x, min.y));
            Place(1, new Vector2(max.x, min.y));
            Place(2, new Vector2(min.x, max.y));
            Place(3, new Vector2(max.x, max.y));
            Show(true);
        }

        /// <summary>A frame's corners on the pixel grid, pushed out by some pixels.</summary>
        public static (Vector2 min, Vector2 max) Snap(Bounds frame, int padding)
        {
            float pad = padding / PixelsPerUnit;
            Vector2 min = new(Mathf.Floor(frame.min.x * PixelsPerUnit) / PixelsPerUnit - pad, Mathf.Floor(frame.min.y * PixelsPerUnit) / PixelsPerUnit - pad);
            Vector2 max = new(Mathf.Ceil(frame.max.x * PixelsPerUnit) / PixelsPerUnit + pad, Mathf.Ceil(frame.max.y * PixelsPerUnit) / PixelsPerUnit + pad);
            return (min, max);
        }

        void Place(int i, Vector2 at) => corners[i].transform.position = new Vector3(at.x, at.y, transform.position.z);

        void Show(bool on)
        {
            foreach (var corner in corners) if (corner != null && corner.enabled != on) corner.enabled = on;
        }

        // An L a pixel thick, its corner at the pivot, so flipping it makes the other three corners round the same point.
        static Sprite MakeBracket(int arm)
        {
            var texture = new Texture2D(arm, arm, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[arm * arm];
            for (int i = 0; i < arm; i++)
            {
                pixels[i] = new Color32(255, 255, 255, 255);            // the bottom row
                pixels[i * arm] = new Color32(255, 255, 255, 255);      // the left column
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, arm, arm), Vector2.zero, PixelsPerUnit);
        }
    }
}
