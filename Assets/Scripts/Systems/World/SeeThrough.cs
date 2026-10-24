using UnityEngine;

namespace BellwortBurrow.Systems.World
{
    /// <summary>
    /// The tall part of a piece, like a log support's beam, that steps aside so it never hides the player or fills the
    /// screen on its own. The rest of the piece (the support's base and ring) always shows.
    /// - The piece's base off the screen: the beam is gone, so a beam never stands on the screen without its foot.
    /// - The player behind the beam (north of the piece's back, within its sides, and covered by it): see-through.
    /// - Otherwise: solid.
    /// The back is the far edge of the parent's collider, or the parent's feet if it has none; the base is the parent's
    /// picture. The screen is the main camera's. The player is whatever carries the Player tag. Setup puts one on every
    /// log support's beam (see SheetPrefabMaker).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SeeThrough : MonoBehaviour
    {
        /// <summary>How the beam shows.</summary>
        public enum Look { Solid, SeeThrough, Gone }

        [Tooltip("How much of the beam shows while the player is behind it (0 gone, 1 solid).")]
        [Range(0f, 1f)] public float behindAlpha = 0.5f;
        [Tooltip("How much of the beam shows while the piece's base is off the screen (0 gone, 1 solid).")]
        [Range(0f, 1f)] public float seeThroughAlpha = 0f;
        [Tooltip("How long it takes to fade out or back in, in seconds.")]
        [Min(0f)] public float fadeTime = 0.2f;
        [Tooltip("How far past the beam's sides the player still counts as behind it, in units (about half the player's width).")]
        [Min(0f)] public float margin = 0.5f;
        [Tooltip("How tall the player stands, in units: how far above their feet the beam can cover them.")]
        [Min(0f)] public float playerHeight = 2f;

        SpriteRenderer picture, foot;
        Transform player;
        Camera view;
        float alpha = 1f;

        void Awake()
        {
            picture = GetComponent<SpriteRenderer>();
            if (transform.parent != null) foot = transform.parent.GetComponent<SpriteRenderer>();
        }

        void LateUpdate()
        {
            if (view == null) view = Camera.main;
            if (player == null)
            {
                var tagged = GameObject.FindWithTag("Player");
                if (tagged != null) player = tagged.transform;
            }
            if (view == null) return;

            float back = transform.parent != null ? transform.parent.position.y : transform.position.y;
            var ground = transform.parent != null ? transform.parent.GetComponent<Collider2D>() : null;
            if (ground != null) back = ground.bounds.max.y;
            var beam = RectOf(picture.bounds);
            var baseRect = foot != null ? RectOf(foot.bounds) : new Rect(beam.xMin, beam.yMin, beam.width, 0.01f);
            float halfHeight = view.orthographicSize, halfWidth = halfHeight * view.aspect;
            Vector2 centre = view.transform.position;
            var screen = new Rect(centre.x - halfWidth, centre.y - halfHeight, 2 * halfWidth, 2 * halfHeight);
            Vector2 feet = player != null ? (Vector2)player.position : new Vector2(float.MaxValue, float.MaxValue);

            var look = LookFor(screen, baseRect, feet, back, beam, margin, playerHeight);
            float target = look == Look.Gone ? seeThroughAlpha : look == Look.SeeThrough ? behindAlpha : 1f;
            alpha = fadeTime > 0f ? Mathf.MoveTowards(alpha, target, Time.deltaTime / fadeTime) : target;
            var color = picture.color;
            if (!Mathf.Approximately(color.a, alpha)) { color.a = alpha; picture.color = color; }
        }

        static Rect RectOf(Bounds b) => new Rect(b.min.x, b.min.y, b.size.x, b.size.y);

        /// <summary>
        /// How the beam shows: gone while the piece's <paramref name="baseRect"/> is off the <paramref name="screen"/>,
        /// see-through while it covers a player standing at <paramref name="feet"/> behind the piece, solid otherwise.
        /// </summary>
        public static Look LookFor(Rect screen, Rect baseRect, Vector2 feet, float pieceBack, Rect beam, float margin, float height)
        {
            if (!screen.Overlaps(baseRect)) return Look.Gone;
            return Covers(feet, pieceBack, beam, margin, height) ? Look.SeeThrough : Look.Solid;
        }

        /// <summary>
        /// Whether the beam covers a player standing at <paramref name="feet"/>: they're behind the piece (north of its
        /// back), below the beam's top, within <paramref name="margin"/> of its sides, and some of them (as tall as
        /// <paramref name="height"/>) is in front of it.
        /// </summary>
        public static bool Covers(Vector2 feet, float pieceBack, Rect beam, float margin, float height) =>
            feet.y > pieceBack && feet.y < beam.yMax && feet.y + height > beam.yMin &&
            feet.x > beam.xMin - margin && feet.x < beam.xMax + margin;
    }
}
