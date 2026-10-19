using UnityEngine;

namespace BellwortBurrow.Systems.World
{
    /// <summary>
    /// Roots a placed piece on whole map tiles. The piece's feet (its sprite pivot, at the bottom middle) stand on the
    /// bottom edge of its base tiles, and the bottom-left corner of its leftmost base tile always lands on whole numbers,
    /// so the piece lines up with the ground tilemap (one tile is one unit). In the editor the piece snaps whenever it is
    /// moved; Bellwort Burrow > Grid > Snap Everything In Open Scenes forces it on every piece at once.
    /// A piece inside another footprint (a piece of a stacked tree) follows its parent instead.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class GridFootprint : MonoBehaviour
    {
        [Tooltip("How many tiles wide the piece's base is, where it meets the map.")]
        [Min(1)] public int tilesWide = 1;

        /// <summary>The bottom-left corner of the leftmost base tile for feet at <paramref name="feet"/>.</summary>
        public Vector2 LeftTileCorner(Vector3 feet) => new Vector2(feet.x - tilesWide * 0.5f, feet.y);

        /// <summary>Where the feet go so the leftmost base tile's corner sits on whole numbers.</summary>
        public Vector3 Snapped(Vector3 feet)
        {
            Vector2 corner = LeftTileCorner(feet);
            return new Vector3(Mathf.Floor(corner.x + 0.5f) + tilesWide * 0.5f, Mathf.Floor(corner.y + 0.5f), feet.z);
        }

        /// <summary>False for a footprint inside another one, which moves with its parent.</summary>
        public bool IsOutermost()
        {
            for (Transform t = transform.parent; t != null; t = t.parent)
                if (t.GetComponent<GridFootprint>() != null) return false;
            return true;
        }

        /// <summary>Moves the piece onto the grid. Returns true if it had to move.</summary>
        public bool Snap()
        {
            if (!IsOutermost()) return false;
            Vector3 position = transform.position, snapped = Snapped(position);
            if ((snapped - position).sqrMagnitude < 1e-10f) return false;
            transform.position = snapped;
            return true;
        }

#if UNITY_EDITOR
        void Update()
        {
            if (Application.isPlaying || !transform.hasChanged) return;
            transform.hasChanged = false;
            if (!gameObject.scene.IsValid() || UnityEditor.EditorUtility.IsPersistent(gameObject)) return;
            if (UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(gameObject) != null) return;
            if (Snap())
            {
                UnityEditor.EditorUtility.SetDirty(transform);
                if (gameObject.scene.IsValid())
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
        }

        void OnDrawGizmosSelected()
        {
            if (!IsOutermost()) return;
            Vector2 corner = LeftTileCorner(transform.position);
            Gizmos.color = new Color(0.85f, 0.65f, 0.13f, 1f);
            for (int i = 0; i < tilesWide; i++)
                Gizmos.DrawWireCube(new Vector3(corner.x + i + 0.5f, corner.y + 0.5f, 0f), new Vector3(1f, 1f, 0f));
        }
#endif
    }
}
