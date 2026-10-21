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

                EditorUtility.DisplayProgressBar("Bellwort Burrow", "Painting the ground tile palettes", 0.85f);
                var palettes = GroundPalette.MakeAll();

                if (palettes != null)
                {
                    // Remember the Glow layers and slices this run was built from, so Check Art Rules can tell when it's out of date.
                    SetupStamp.Write();
                    Debug.Log($"Bellwort Burrow: forest art set up. {prefabs} prefabs, {trees} tall trees, {groundTiles} ground tiles and the " +
                              $"{string.Join(" and ", palettes)} tile palette{(palettes.Count == 1 ? "" : "s")}.");
                }
                else
                    Debug.LogError($"Bellwort Burrow: forest art setup finished with problems ({prefabs} prefabs, {trees} tall trees, " +
                                   $"{groundTiles} ground tiles, a tile palette not made). See the messages above.");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }
    }
}
