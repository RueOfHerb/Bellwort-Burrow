using UnityEditor;
using UnityEngine;

namespace BellwortBurrow.EditorTools
{
    /// <summary>
    /// One click from the art package to art you can place by hand: Bellwort Burrow > Setup > Set Up Forest Art (All Steps).
    /// Each step is also on its own under Bellwort Burrow > Setup > Forest Steps. None of them touch a scene;
    /// scenes are built by hand with prefabs and the Tile Palette.
    /// </summary>
    public static class ForestSetup
    {
        [MenuItem("Bellwort Burrow/Setup/Set Up Forest Art (All Steps)", false, 100)]
        public static void RunAll()
        {
            if (!ArtLibrary.CheckArtInstalled()) return;

            try
            {
                EditorUtility.DisplayProgressBar("Bellwort Burrow", "Applying pixel art settings", 0.1f);
                PixelArtProjectSettings.Apply();

                EditorUtility.DisplayProgressBar("Bellwort Burrow", "Making prefabs from the sheets", 0.35f);
                int prefabs = SheetPrefabMaker.MakeAll();

                EditorUtility.DisplayProgressBar("Bellwort Burrow", "Stacking tall trees", 0.55f);
                int trees = TallTreeBuilder.MakeAll();

                EditorUtility.DisplayProgressBar("Bellwort Burrow", "Making the ground auto tiles", 0.7f);
                int groundTiles = GroundAutoTiles.MakeAll();

                EditorUtility.DisplayProgressBar("Bellwort Burrow", "Painting the ground tile palette", 0.85f);
                bool palette = ForestGroundPalette.Make();

                if (palette)
                    Debug.Log($"Bellwort Burrow: forest art set up. {prefabs} prefabs, {trees} tall trees, {groundTiles} ground tiles and the Forest Ground tile palette.");
                else
                    Debug.LogError($"Bellwort Burrow: forest art setup finished with problems ({prefabs} prefabs, {trees} tall trees, " +
                                   $"{groundTiles} ground tiles, palette not made). See the messages above.");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }
    }
}
