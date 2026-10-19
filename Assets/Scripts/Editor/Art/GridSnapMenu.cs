using BellwortBurrow.Systems.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BellwortBurrow.EditorTools
{
    /// <summary>
    /// Bellwort Burrow > Grid > Snap Everything In Open Scenes: forces every placed piece onto the map grid at once
    /// (the bottom-left corner of its leftmost base tile on whole numbers, see <see cref="GridFootprint"/>) and puts every
    /// tree house piece on its tree (see <see cref="TreeHouse"/>). Pieces also snap on their own whenever they're moved;
    /// this is for pieces placed before snapping existed, or to check a scene. It can be undone.
    /// </summary>
    public static class GridSnapMenu
    {
        [MenuItem("Bellwort Burrow/Grid/Snap Everything In Open Scenes", false, 300)]
        static void SnapFromMenu()
        {
            var (moved, attached) = SnapOpenScenes();
            Debug.Log($"Bellwort Burrow: snapped {moved} pieces to the grid and put {attached} tree house pieces on their trees.");
        }

        /// <summary>Snaps every piece in the open scenes. Returns how many moved and how many tree house pieces moved.</summary>
        public static (int moved, int attached) SnapOpenScenes()
        {
            int moved = 0, attached = 0;
            foreach (var foot in Object.FindObjectsByType<GridFootprint>(FindObjectsInactive.Include))
            {
                if (EditorUtility.IsPersistent(foot) || !foot.IsOutermost()) continue;
                Undo.RecordObject(foot.transform, "Snap to grid");
                if (!foot.Snap()) continue;
                moved++;
                EditorSceneManager.MarkSceneDirty(foot.gameObject.scene);
            }
            foreach (var house in Object.FindObjectsByType<TreeHouse>(FindObjectsInactive.Include))
            {
                if (EditorUtility.IsPersistent(house)) continue;
                Undo.RecordObject(house.transform, "Put tree house on tree");
                if (!house.Attach()) continue;
                attached++;
                EditorSceneManager.MarkSceneDirty(house.gameObject.scene);
            }
            return (moved, attached);
        }
    }
}
