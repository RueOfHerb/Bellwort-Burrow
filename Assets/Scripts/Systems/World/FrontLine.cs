using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace BellwortBurrow.Systems.World
{
    /// <summary>
    /// The front edge of a round piece's ground, like the great stump's roots (fix, Oct 27). Pieces sort by their
    /// feet, the lowest point of their front, so a player standing in front of a root that curves back at the side
    /// (their feet higher than the piece's feet, but still nearer the camera than the root) would be drawn behind the
    /// whole piece. While the player's feet are in front of this line, the piece draws one step under them instead,
    /// and goes back as soon as they aren't. The line is the front of the piece's collider, so nobody stands inside
    /// it. Setup puts one on every round piece (see SheetPrefabMaker).
    /// </summary>
    public sealed class FrontLine : MonoBehaviour
    {
        [Tooltip("The front edge, left to right, in units from the piece's feet (y up). Setup fills it in.")]
        public Vector2[] line = Array.Empty<Vector2>();

        SortingGroup group;
        SpriteRenderer picture;
        Transform player;
        int normal;
        bool lowered;

        void Awake()
        {
            group = GetComponent<SortingGroup>();
            picture = GetComponent<SpriteRenderer>();
            normal = group != null ? group.sortingOrder : picture != null ? picture.sortingOrder : 0;
        }

        void OnDisable() => Lower(false);

        void LateUpdate()
        {
            if (player == null)
            {
                var tagged = GameObject.FindWithTag("Player");
                if (tagged != null) player = tagged.transform;
            }
            Lower(player != null && InFront(line, (Vector2)(player.position - transform.position)));
        }

        void Lower(bool on)
        {
            if (on == lowered) return;
            lowered = on;
            int order = on ? normal - 1 : normal;
            if (group != null) group.sortingOrder = order;
            else if (picture != null) picture.sortingOrder = order;
        }

        /// <summary>
        /// Whether <paramref name="feet"/> (from the piece's feet, in units) stand in front of the line where sorting by
        /// height alone would put them behind the piece: above its feet row, below the line, within its width.
        /// </summary>
        public static bool InFront(Vector2[] line, Vector2 feet)
        {
            if (line == null || line.Length < 2) return false;
            float xMin = Mathf.Min(line[0].x, line[line.Length - 1].x);
            float xMax = Mathf.Max(line[0].x, line[line.Length - 1].x);
            if (feet.x < xMin || feet.x > xMax) return false;
            return feet.y > 0f && feet.y < Bridge.HeightAt(line, feet.x);
        }
    }
}
