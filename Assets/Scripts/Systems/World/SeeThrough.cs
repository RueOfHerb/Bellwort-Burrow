using UnityEngine;

namespace BellwortBurrow.Systems.World
{
    /// <summary>
    /// A tall pillar, like a log support, that turns see-through while the player is north of it and fades away once
    /// they're a few tiles further north, so it never hides them. Only how far north counts, not how far to the side:
    /// the player anywhere in the pillar's band of the map, from the back of the piece up to the beam's top, fades it.
    /// - Just north of the back: the whole pillar (the beam, its base and ring) goes see-through.
    /// - More than <see cref="fadeAfterTiles"/> north of the back: the whole pillar is gone.
    /// - South of the back or above the beam's top: solid.
    /// The back is the far edge of the parent's collider, or the parent's feet if it has none. The player is whatever
    /// carries the Player tag. Setup puts one on every log support's beam (see SheetPrefabMaker).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SeeThrough : MonoBehaviour
    {
        /// <summary>How a pillar shows for where the player stands.</summary>
        public enum Look { Solid, SeeThrough, Gone }

        [Tooltip("How much of the pillar shows while the player is just north of it (0 gone, 1 solid).")]
        [Range(0f, 1f)] public float behindAlpha = 0.5f;
        [Tooltip("How much of the pillar shows once the player is further north (0 gone, 1 solid).")]
        [Range(0f, 1f)] public float seeThroughAlpha = 0f;
        [Tooltip("How many tiles north of the back of the piece (the far edge of its collider) the player walks before the pillar is gone.")]
        [Min(0f)] public float fadeAfterTiles = 2f;
        [Tooltip("How long it takes to fade out or back in, in seconds.")]
        [Min(0f)] public float fadeTime = 0.2f;

        SpriteRenderer[] parts = new SpriteRenderer[0];
        float[] alphas = new float[0];
        Transform player;

        void Awake()
        {
            // the beam, then the rest of the piece: its base and the other parts beside the beam
            var found = new System.Collections.Generic.List<SpriteRenderer> { GetComponent<SpriteRenderer>() };
            if (transform.parent != null)
            {
                var own = transform.parent.GetComponent<SpriteRenderer>();
                if (own != null) found.Add(own);
                foreach (Transform sibling in transform.parent)
                    if (sibling != transform && sibling.TryGetComponent<SpriteRenderer>(out var part)) found.Add(part);
            }
            parts = found.ToArray();
            alphas = new float[parts.Length];
            for (int i = 0; i < alphas.Length; i++) alphas[i] = 1f;
        }

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
            float top = parts[0].bounds.max.y;

            var look = LookFor(player.position.y, back, top, fadeAfterTiles);
            float target = look == Look.Gone ? seeThroughAlpha : look == Look.SeeThrough ? behindAlpha : 1f;
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == null) continue;
                alphas[i] = fadeTime > 0f ? Mathf.MoveTowards(alphas[i], target, Time.deltaTime / fadeTime) : target;
                var color = parts[i].color;
                if (!Mathf.Approximately(color.a, alphas[i])) { color.a = alphas[i]; parts[i].color = color; }
            }
        }

        /// <summary>
        /// How the pillar shows for a player whose feet are at <paramref name="feetY"/>: solid south of the piece's back
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
