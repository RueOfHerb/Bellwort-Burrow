using UnityEngine;

namespace BellwortBurrow.Systems.World
{
    /// <summary>Which tree houses fit a giant trunk: the wood trunks (brown, wiggle, stout) or the braided vine.</summary>
    public enum TreeFit { Wood, Vine }

    /// <summary>
    /// A stacked giant tree that tree houses can go on (see <see cref="TreeHouse"/>). Made by the Stack Tall Trees step,
    /// which records where each trunk piece meets the next: an upper room sits on one of those joins, where the trunk
    /// runs straight up the middle.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GiantTree : MonoBehaviour
    {
        [Tooltip("Which tree houses fit this trunk.")]
        public TreeFit fit;

        [Tooltip("Heights above the feet, in units, where one trunk piece meets the next (lowest first).")]
        public float[] joins = new float[0];

        /// <summary>The lowest join, where the standard stairs reach a room. 0 when the tree has none.</summary>
        public float FirstJoin => joins != null && joins.Length > 0 ? joins[0] : 0f;

        /// <summary>The join closest to a height above the feet.</summary>
        public float NearestJoin(float height)
        {
            if (joins == null || joins.Length == 0) return 0f;
            float best = joins[0];
            foreach (float join in joins)
                if (Mathf.Abs(join - height) < Mathf.Abs(best - height)) best = join;
            return best;
        }
    }
}
