using System.Collections.Generic;
using UnityEngine;

namespace BellwortBurrow.Systems.World
{
    /// <summary>
    /// The tall part of a piece, like a log support's beam, that steps aside so it never hides the player or stands on
    /// the screen without its foot. The rest of the piece (the support's base and ring) always shows.
    /// - The player behind the beam (north of the piece's back, some of them covered by it): see-through.
    /// - The piece's base leaving the screen: the beam fades with it, as much of the beam showing as of the base, and is
    ///   gone once the base is off the screen.
    /// - Otherwise: solid.
    /// It fades by dithering, not by blending: it shows only some of its pixels, in a 4 by 4 checkered pattern (its
    /// <see cref="dither"/> steps, made by the importer), so every pixel on screen keeps a colour from the palette.
    /// The back is the far edge of the parent's collider, or the parent's feet if it has none; the base is the parent's
    /// picture. The screen is the main camera's. The player is whatever carries the Player tag, as much of it as its
    /// pictures cover. Setup puts one on every log support's beam (see SheetPrefabMaker).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SeeThrough : MonoBehaviour
    {
        /// <summary>How many steps a dither has from gone to solid: 16, one per pixel of the 4 by 4 pattern.</summary>
        public const int DitherSteps = 16;

        static readonly int[] Bayer = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

        [Tooltip("How much of the beam shows while the player is behind it (0 gone, 1 solid).")]
        [Range(0f, 1f)] public float behindAlpha = 0.5f;
        [Tooltip("How long it takes to turn see-through or back, in seconds.")]
        [Min(0f)] public float fadeTime = 0.2f;
        [Tooltip("The beam with fewer and fewer of its pixels, lightest first: 1 in 16 up to 15 in 16. Setup fills it in. " +
                 "Empty: the beam fades by blending instead.")]
        public Sprite[] dither = new Sprite[0];

        SpriteRenderer picture, foot;
        Sprite solid;
        Transform player;
        readonly List<SpriteRenderer> body = new List<SpriteRenderer>();
        Camera view;
        float cover = 1f;

        void Awake()
        {
            picture = GetComponent<SpriteRenderer>();
            solid = picture.sprite;
            if (transform.parent != null) foot = transform.parent.GetComponent<SpriteRenderer>();
        }

        void LateUpdate()
        {
            if (view == null) view = Camera.main;
            if (player == null)
            {
                var tagged = GameObject.FindWithTag("Player");
                if (tagged != null) { player = tagged.transform; player.GetComponentsInChildren(body); }
            }
            if (view == null || solid == null) return;

            float back = transform.parent != null ? transform.parent.position.y : transform.position.y;
            var ground = transform.parent != null ? transform.parent.GetComponent<Collider2D>() : null;
            if (ground != null) back = ground.bounds.max.y;
            var beam = new Rect((Vector2)transform.position + (Vector2)solid.bounds.min, solid.bounds.size);
            var baseRect = foot != null ? RectOf(foot.bounds) : new Rect(beam.xMin, beam.yMin, beam.width, 0.01f);
            float halfHeight = view.orthographicSize, halfWidth = halfHeight * view.aspect;
            Vector2 centre = view.transform.position;
            var screen = new Rect(centre.x - halfWidth, centre.y - halfHeight, 2 * halfWidth, 2 * halfHeight);

            bool behind = player != null && Covers(player.position, BodyRect(), back, beam);
            float target = behind ? behindAlpha : 1f;
            cover = fadeTime > 0f ? Mathf.MoveTowards(cover, target, Time.deltaTime / fadeTime) : target;
            Show(cover * Shown(screen, baseRect));
        }

        void Show(float alpha)
        {
            if (dither == null || dither.Length != DitherSteps - 1)
            {
                // no dither: blend
                picture.enabled = alpha > 0f;
                var color = picture.color;
                if (!Mathf.Approximately(color.a, alpha)) { color.a = alpha; picture.color = color; }
                return;
            }
            int step = StepFor(alpha);
            picture.enabled = step > 0;
            var sprite = step >= DitherSteps ? solid : step > 0 ? dither[step - 1] : picture.sprite;
            if (picture.sprite != sprite) picture.sprite = sprite;
        }

        Rect BodyRect()
        {
            bool any = false;
            var bounds = new Bounds();
            foreach (var r in body)
            {
                if (r == null || !r.enabled || r.sprite == null) continue;
                if (any) bounds.Encapsulate(r.bounds); else { bounds = r.bounds; any = true; }
            }
            if (!any) return new Rect((Vector2)player.position - new Vector2(0.5f, 0f), new Vector2(1f, 2f));
            return RectOf(bounds);
        }

        static Rect RectOf(Bounds b) => new Rect(b.min.x, b.min.y, b.size.x, b.size.y);

        /// <summary>
        /// Whether the beam covers the player: their feet are behind the piece (north of its back) and below the beam's
        /// top, and their <paramref name="body"/> (what their pictures cover) overlaps it.
        /// </summary>
        public static bool Covers(Vector2 feet, Rect body, float pieceBack, Rect beam) =>
            feet.y > pieceBack && feet.y < beam.yMax && body.Overlaps(beam);

        /// <summary>How much of the piece's base is on the screen, 0 to 1: how much of the beam may show.</summary>
        public static float Shown(Rect screen, Rect baseRect)
        {
            if (baseRect.width <= 0f || baseRect.height <= 0f) return screen.Overlaps(baseRect) ? 1f : 0f;
            float w = Mathf.Max(0f, Mathf.Min(screen.xMax, baseRect.xMax) - Mathf.Max(screen.xMin, baseRect.xMin));
            float h = Mathf.Max(0f, Mathf.Min(screen.yMax, baseRect.yMax) - Mathf.Max(screen.yMin, baseRect.yMin));
            return (w / baseRect.width) * (h / baseRect.height);
        }

        /// <summary>The dither step for how much of the beam shows: 0 gone, <see cref="DitherSteps"/> solid.</summary>
        public static int StepFor(float alpha) => Mathf.Clamp(Mathf.RoundToInt(alpha * DitherSteps), 0, DitherSteps);

        /// <summary>
        /// The 4 by 4 ordered pattern: a pixel shows from step threshold + 1 on, so each step adds one pixel in 16 spread
        /// evenly. <paramref name="x"/> and <paramref name="y"/> count from the picture's bottom left corner.
        /// </summary>
        public static int DitherThreshold(int x, int y) => Bayer[(y & 3) * 4 + (x & 3)];
    }
}
