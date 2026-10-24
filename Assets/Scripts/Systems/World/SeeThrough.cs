using UnityEngine;

namespace BellwortBurrow.Systems.World
{
    /// <summary>
    /// The tall part of a piece, like a log support's beam, that turns see-through while the player is north of the piece
    /// and fades away once they're a few tiles further north, so it never hides them. The rest of the piece (the support's
    /// base and ring) stays solid. Only how far north counts, not how far to the side: the player anywhere in the beam's
    /// band of the map, from the back of the piece up to the beam's top, fades it.
    /// - Just north of the back: the beam goes see-through.
    /// - More than <see cref="fadeAfterTiles"/> north of the back: the beam is gone.
    /// - South of the back or above the beam's top: solid.
    /// The back is the far edge of the parent's collider, or the parent's feet if it has none. The player is whatever
    /// carries the Player tag. Setup puts one on every log support's beam (see SheetPrefabMaker).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SeeThrough : MonoBehaviour
    {
        /// <summary>How the beam shows for where the player stands.</summary>
        public enum Look { Solid, SeeThrough, Gone }

        [Tooltip("How much of the beam shows while the player is just north of the piece (0 gone, 1 solid).")]
        [Range(0f, 1f)] public float behindAlpha = 0.5f;
        [Tooltip("How much of the beam shows once the player is further north (0 gone, 1 solid).")]
        [Range(0f, 1f)] public float seeThroughAlpha = 0f;
        [Tooltip("How many tiles north of the back of the piece (the far edge of its collider) the player walks before the beam is gone.")]
        [Min(0f)] public float fadeAfterTiles = 2f;
        [Tooltip("How long it takes to fade out or back in, in seconds.")]
        [Min(0f)] public float fadeTime = 0.2f;

        SpriteRenderer picture;
        Transform player;
        float alpha = 1f;

        void Awake() => picture = GetComponent<SpriteRenderer>();

        void LateUpdate()
        {
            if (player == null)
            {
                var tagged = GameObject.FindWithTag("Player");
                if (tagged == null) return;
                player = tagged.transform;
            }
            float back = transform.parent != null ? transform.parent.position.y : transform.position.y;
            var ground = transform.parent != null ? transform.parent.GetComponent<Collider2D>() : null;
            if (ground != null) back = ground.bounds.max.y;
            float top = picture.bounds.max.y;

            var look = LookFor(player.position.y, back, top, fadeAfterTiles);
            float target = look == Look.Gone ? seeThroughAlpha : look == Look.SeeThrough ? behindAlpha : 1f;
            alpha = fadeTime > 0f ? Mathf.MoveTowards(alpha, target, Time.deltaTime / fadeTime) : target;
            var color = picture.color;
            if (!Mathf.Approximately(color.a, alpha)) { color.a = alpha; picture.color = color; }
        }

        /// <summary>
        /// How the beam shows for a player whose feet are at <paramref name="feetY"/>: solid south of the piece's back
        /// or above the beam's top, see-through just north of the back, gone once they're more than
        /// <paramref name="afterTiles"/> north of it.
        /// </summary>
        public static Look LookFor(float feetY, float pieceBack, float beamTop, float afterTiles)
        {
            if (feetY <= pieceBack || feetY >= beamTop) return Look.Solid;
            return feetY > pieceBack + afterTiles ? Look.Gone : Look.SeeThrough;
        }
    }
}
