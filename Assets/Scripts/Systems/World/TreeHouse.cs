using UnityEngine;

namespace BellwortBurrow.Systems.World
{
    /// <summary>
    /// A tree house piece that goes in front of a giant tree's trunk. Drop it near a giant tree that it fits and it
    /// becomes part of that tree: a ground house sits at the tree's feet, an upper room on the join nearest to where it
    /// was dropped, and stairs on the lowest join (they reach from a room there down to the ground house roof).
    /// It draws in front of the trunk because it sorts inside the tree (see <see cref="SortingOrderFor"/>).
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class TreeHouse : MonoBehaviour
    {
        public enum Part { House, Room, Stairs }

        [Tooltip("Ground house, upper room or stairs.")]
        public Part part;

        [Tooltip("The trunks this piece is drawn for.")]
        public TreeFit fit;

        /// <summary>How far away, in units, a giant tree can be and still take the piece.</summary>
        public const float Reach = 4f;

        /// <summary>
        /// The piece's order in the tree's sorting group: in front of every trunk piece (they use 0 and up from the base),
        /// stairs in front of the roofs. A piece's glow uses the next number. These are plain renderer orders, not a
        /// sorting group of its own, because a sorting group nested in the tree's drew behind the trunk.
        /// </summary>
        public static int SortingOrderFor(Part part) => part == Part.House ? 100 : part == Part.Room ? 110 : 120;

        /// <summary>Puts the piece on the nearest giant tree that fits, at its spot. Returns true if it had to move.</summary>
        public bool Attach()
        {
            GiantTree tree = transform.parent != null ? transform.parent.GetComponent<GiantTree>() : null;
            GiantTree nearest = Nearest();
            if (nearest != null && nearest != tree && (tree == null || Distance(nearest) < Distance(tree) - 0.5f)) tree = nearest;
            if (tree == null || tree.fit != fit) return false;

            bool moved = false;
            if (transform.parent != tree.transform)
            {
                moved = true;
#if UNITY_EDITOR
                if (!Application.isPlaying) UnityEditor.Undo.SetTransformParent(transform, tree.transform, "Put tree house on tree");
                else
#endif
                    transform.SetParent(tree.transform, true);
            }
            float height = tree.transform.InverseTransformPoint(transform.position).y;
            float y = part == Part.House ? 0f : part == Part.Stairs ? tree.FirstJoin : tree.NearestJoin(height);
            var target = new Vector3(0f, y, 0f);
            if ((transform.localPosition - target).sqrMagnitude > 1e-10f) { transform.localPosition = target; moved = true; }
            return moved;
        }

        float Distance(GiantTree tree) => Mathf.Abs(tree.transform.position.x - transform.position.x);

        GiantTree Nearest()
        {
            GiantTree best = null;
            float bestDistance = Reach;
            foreach (var tree in FindObjectsByType<GiantTree>())
            {
                if (tree.fit != fit || tree.gameObject.scene != gameObject.scene) continue;
                float d = Distance(tree);
                if (d <= bestDistance) { best = tree; bestDistance = d; }
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
