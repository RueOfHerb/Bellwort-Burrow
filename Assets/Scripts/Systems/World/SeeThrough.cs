using UnityEngine;

namespace BellwortBurrow.Systems.World
{
    /// <summary>
    /// A tall part of a piece, like a log support's beam, that turns see-through while the player walks behind it, so
    /// they never vanish from view, and comes back once they step out. The rest of the piece (the support's base) stays
    /// as it is. The piece's feet are its parent's position; the player is whatever carries the Player tag.
    /// Setup puts one on every log support's beam (see SheetPrefabMaker).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SeeThrough : MonoBehaviour
    {
        [Tooltip("How much of the beam shows while the player is behind it (0 gone, 1 solid).")]
        [Range(0f, 1f)] public float seeThroughAlpha = 0.35f;
        [Tooltip("How long it takes to fade out or back in, in seconds.")]
        [Min(0f)] public float fadeTime = 0.2f;
        [Tooltip("How far past the beam's sides the player still counts as behind it, in units (about half the player's width).")]
        [Min(0f)] public float margin = 0.75f;

        SpriteRenderer picture;
        Transform player;
        float alpha = 1f;

        void Awake() => picture = GetComponent<SpriteRenderer>();

        void LateUpdate()
        {
            if (player == null)
            {
                var found = GameObject.FindWithTag("Player");
                if (found == null) return;
                player = found.transform;
            }
            var bounds = picture.bounds;
            float feet = transform.parent != null ? transform.parent.position.y : transform.position.y;
            var beam = new Rect(bounds.min.x, bounds.min.y, bounds.size.x, bounds.size.y);
            float target = IsBehind(player.position, feet, beam, margin) ? seeThroughAlpha : 1f;
            alpha = fadeTime > 0f ? Mathf.MoveTowards(alpha, target, Time.deltaTime / fadeTime) : target;
            var color = picture.color;
            if (!Mathf.Approximately(color.a, alpha)) { color.a = alpha; picture.color = color; }
        }

        /// <summary>
        /// Whether a player standing at <paramref name="feet"/> is behind a beam: further back (north) than the piece's
        /// own feet, below the beam's top, and within <paramref name="margin"/> of its sides.
        /// </summary>
        public static bool IsBehind(Vector2 feet, float pieceFeet, Rect beam, float margin) =>
            feet.y > pieceFeet && feet.y < beam.yMax && feet.x > beam.xMin - margin && feet.x < beam.xMax + margin;
    }
}
