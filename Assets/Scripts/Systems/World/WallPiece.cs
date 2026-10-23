using System.Collections.Generic;
using UnityEngine;

namespace BellwortBurrow.Systems.World
{
    /// <summary>
    /// A piece that goes on a building's wall, as a sprite of its own: the ladder and the lantern hook on the great
    /// stump. Drop it anywhere on a <see cref="WallSeat"/>'s picture and it becomes part of that building, keeping the
    /// spot it was dropped on, and draws in front of the building's wall because it sorts inside the building's sorting
    /// group (see <see cref="SortingOrder"/>). Its pivot is the bottom of its own picture (the ladder's feet). Delete it
    /// to leave the wall as it was.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class WallPiece : MonoBehaviour
    {
        /// <summary>
        /// The piece's order in its building's sorting group: in front of the building (0) and its glow (1). A plain
        /// renderer order, not a sorting group of its own, so it stays part of the building when things sort by height.
        /// </summary>
        public const int SortingOrder = 100;

        /// <summary>Puts the piece on the building whose picture it was dropped on. Returns true if it had to move.</summary>
        public bool Attach()
        {
            var seats = new List<WallSeat>();
            var walls = new List<Rect>();
            foreach (var seat in FindObjectsByType<WallSeat>())
            {
                if (seat.gameObject.scene != gameObject.scene) continue;
                var picture = seat.GetComponent<SpriteRenderer>();
                if (picture == null || picture.sprite == null) continue;
                var b = picture.bounds;
                seats.Add(seat);
                walls.Add(new Rect(b.min.x, b.min.y, b.size.x, b.size.y));
            }
            int on = Pick(transform.position, walls);
            if (on < 0 || transform.parent == seats[on].transform) return false;
#if UNITY_EDITOR
            if (!Application.isPlaying) UnityEditor.Undo.SetTransformParent(transform, seats[on].transform, "Put piece on wall");
            else
#endif
                transform.SetParent(seats[on].transform, true);
            return true;
        }

        /// <summary>
        /// The wall a point is on: of the pictures that hold it, the one whose middle is nearest. -1 when none does.
        /// </summary>
        public static int Pick(Vector2 at, IList<Rect> walls)
        {
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < walls.Count; i++)
            {
                if (!walls[i].Contains(at)) continue;
                float d = Vector2.Distance(at, walls[i].center);
                if (d < bestDistance) { best = i; bestDistance = d; }
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
