using UnityEngine;

namespace BellwortBurrow.Systems.World
{
    /// <summary>
    /// A tall part of a piece, like a log support's beam, that fades away once the player has walked a few tiles in
    /// behind it, so they never stay lost behind it, and comes back once they step out. Just behind it, it stays solid.
    /// The rest of the piece (the support's base) stays as it is. The tiles count from the back of the piece: the far
    /// edge of its parent's collider, or its parent's feet if it has none. The player is whatever carries the Player tag.
    /// Setup puts one on every log support's beam (see SheetPrefabMaker).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SeeThrough : MonoBehaviour
    {
        [Tooltip("How much of the beam shows once it has faded (0 gone, 1 solid).")]
        [Range(0f, 1f)] public float seeThroughAlpha = 0f;
        [Tooltip("How many tiles past the back of the piece (the far edge of its collider) the player walks behind it before it fades.")]
        [Min(0f)] public float fadeAfterTiles = 2f;
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
            float back = transform.parent != null ? transform.parent.position.y : transform.position.y;
            var ground = transform.parent != null ? transform.parent.GetComponent<Collider2D>() : null;
            if (ground != null) back = ground.bounds.max.y;
            var beam = new Rect(bounds.min.x, bounds.min.y, bounds.size.x, bounds.size.y);
            float target = IsBehind(player.position, back, beam, margin, fadeAfterTiles) ? seeThroughAlpha : 1f;
            alpha = fadeTime > 0f ? Mathf.MoveTowards(alpha, target, Time.deltaTime / fadeTime) : target;
            var color = picture.color;
            if (!Mathf.Approximately(color.a, alpha)) { color.a = alpha; picture.color = color; }
        }

        /// <summary>
        /// Whether a player standing at <paramref name="feet"/> is far enough behind a beam for it to fade: more than
        /// <paramref name="afterTiles"/> north of the back of the piece, below the beam's top, and within
        /// <paramref name="margin"/> of its sides.
        /// </summary>
        public static bool IsBehind(Vector2 feet, float pieceBack, Rect beam, float margin, float afterTiles = 0f) =>
            feet.y > pieceBack + afterTiles && feet.y < beam.yMax && feet.x > beam.xMin - margin && feet.x < beam.xMax + margin;
    }
}
