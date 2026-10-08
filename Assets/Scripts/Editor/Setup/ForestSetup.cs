using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BellwortBurrow.EditorTools
{
    /// <summary>
    /// One click from the art package to a lit forest scene: Bellwort Burrow > Setup > Set Up Forlorn Forest (All Steps).
    /// Each step is also on its own under Bellwort Burrow > Setup > Forest Steps.
    /// </summary>
    public static class ForestSetup
    {
        [MenuItem("Bellwort Burrow/Setup/Set Up Forlorn Forest (All Steps)", false, 100)]
        public static void RunAll()
        {
            if (!ArtLibrary.CheckArtInstalled()) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            try
            {
                EditorUtility.DisplayProgressBar("Bellwort Burrow", "Applying pixel art settings", 0.1f);
                PixelArtProjectSettings.Apply();

                EditorUtility.DisplayProgressBar("Bellwort Burrow", "Making prefabs from the sheets", 0.3f);
                int prefabs = SheetPrefabMaker.MakeAll();

                EditorUtility.DisplayProgressBar("Bellwort Burrow", "Stacking tall trees", 0.5f);
                int trees = TallTreeBuilder.MakeAll();

                EditorUtility.DisplayProgressBar("Bellwort Burrow", "Painting the ground tile palette", 0.65f);
                bool palette = ForestGroundPalette.Make();

                EditorUtility.DisplayProgressBar("Bellwort Burrow", "Building the Forlorn Forest scene", 0.8f);
                bool built = ForlornForestSceneBuilder.Build();

                if (palette && built)
                    Debug.Log($"Bellwort Burrow: forest set up. {prefabs} prefabs, {trees} tall trees, the Forest Ground tile palette and {ForlornForestSceneBuilder.ScenePath}.");
                else
                    Debug.LogError($"Bellwort Burrow: forest setup finished with problems ({prefabs} prefabs, {trees} tall trees, " +
                                   $"palette {(palette ? "made" : "not made")}, scene {(built ? "built" : "not built")}). See the messages above.");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }
    }
}
