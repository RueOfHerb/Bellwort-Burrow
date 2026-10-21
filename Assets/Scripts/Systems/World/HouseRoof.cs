using System.Collections.Generic;
using UnityEngine;

namespace BellwortBurrow.Systems.World
{
    /// <summary>
    /// A roof that goes on top of a house, as a piece of its own (the Burrow's pointed roofs on its homes). Drop it near
    /// a house it fits (a <see cref="RoofSeat"/> of the same size) and it becomes part of that house: it moves onto the
    /// house's spot, since the roof's pivot is the house's feet, and draws in front of the house's own roof because it
    /// sorts inside the house (see <see cref="SortingOrder"/>). Delete the roof to leave the house as it was.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class HouseRoof : MonoBehaviour
    {
        [Tooltip("The house size this roof fits, like burrow_home.")]
        public string fits;

        /// <summary>How far away, in units, a house can be and still take the roof.</summary>
        public const float Reach = 3f;

        /// <summary>
        /// The roof's order in its house's sorting group: in front of the house (0) and its glow (1). A plain renderer
        /// order, not a sorting group of its own, so the roof stays part of the house when things sort by height.
        /// </summary>
        public const int SortingOrder = 100;

        /// <summary>Puts the roof on the nearest house that fits, on its spot. Returns true if it had to move.</summary>
        public bool Attach()
        {
            RoofSeat seat = transform.parent != null ? transform.parent.GetComponent<RoofSeat>() : null;
            if (seat != null && seat.size != fits) seat = null;
            var seats = new List<RoofSeat>();
            var spots = new List<Vector2>();
            foreach (var candidate in FindObjectsByType<RoofSeat>())
            {
                if (candidate.size != fits || candidate.gameObject.scene != gameObject.scene) continue;
                seats.Add(candidate);
                spots.Add(candidate.transform.position);
            }
            int nearest = Nearest(transform.position, spots, Reach);
            if (nearest >= 0 && seats[nearest] != seat &&
                (seat == null || Vector2.Distance(spots[nearest], transform.position) <
                                 Vector2.Distance(seat.transform.position, transform.position) - 0.5f))
                seat = seats[nearest];
            if (seat == null) return false;

            bool moved = false;
            if (transform.parent != seat.transform)
            {
                moved = true;
#if UNITY_EDITOR
                if (!Application.isPlaying) UnityEditor.Undo.SetTransformParent(transform, seat.transform, "Put roof on house");
                else
#endif
                    transform.SetParent(seat.transform, true);
            }
            if (transform.localPosition.sqrMagnitude > 1e-10f) { transform.localPosition = Vector3.zero; moved = true; }
            return moved;
        }

        /// <summary>The index of the spot nearest to a point and within reach, or -1 when none is.</summary>
        public static int Nearest(Vector2 at, IList<Vector2> spots, float reach)
        {
            int best = -1;
            float bestDistance = reach;
            for (int i = 0; i < spots.Count; i++)
            {
                float d = Vector2.Distance(at, spots[i]);
                if (d <= bestDistance) { best = i; bestDistance = d; }
            }
            return best;
        }

#if UNITY_EDITOR
        void Update()
        {
            if (Application.isPlaying || !transform.hasChanged) return;
            transform.hasChanged = false;
            if (!gameObject.scene.IsValid() || UnityEditor.EditorUtility.IsPersistent(gameObject)) return;
            if (UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(gameObject) != null) return;
            if (Attach())
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
    }
}
