using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace BellwortBurrow.EditorTools
{
    /// <summary>
    /// Checks that what setup makes still matches the sheets. Used before exporting the art package, so a package never
    /// goes out with a slice that has no prefab, or a prefab that points at a slice that's gone.
    /// Redrawing pixels inside existing slices needs no setup run, so that alone is never reported.
    /// </summary>
    public static class ArtChecks
    {
        /// <summary>Problems found, as short lines for a dialog. Empty when everything matches.</summary>
        public static List<string> SetupProblems()
        {
            var problems = new List<string>();

            // Every slice on a sprite sheet has its prefab.
            foreach (var sheet in ArtLibrary.SheetPaths(tileSheets: false))
            {
                string sheetName = Path.GetFileNameWithoutExtension(sheet);
                var sprites = ArtLibrary.LoadSubAssets<Sprite>(sheet);
                if (sprites.Count == 0)
                {
                    problems.Add($"{sheetName} didn't import any sprites (see the Console)");
                    continue;
                }
                string folder = $"{ArtLibrary.PrefabRoot}/{sheetName}";
                int missing = 0;
                string example = null;
                foreach (var name in sprites.Keys)
                {
                    if (name.EndsWith("_glow") && sprites.ContainsKey(name.Substring(0, name.Length - 5))) continue;
                    if (File.Exists($"{folder}/{name}.prefab")) continue;
                    missing++;
                    if (example == null) example = name;
                }
                if (missing > 0) problems.Add($"{sheetName}: {missing} slice(s) have no prefab yet, like {example}");
            }

            // No prefab points at a sprite that's gone (a renamed or removed slice).
            if (AssetDatabase.IsValidFolder(ArtLibrary.PrefabRoot))
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { ArtLibrary.PrefabRoot }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null) continue;
                    foreach (var renderer in prefab.GetComponentsInChildren<SpriteRenderer>(true))
                    {
                        if (renderer.sprite != null) continue;
                        problems.Add($"{Path.GetFileNameWithoutExtension(path)} shows nothing; its slice was renamed or removed");
                        break;
                    }
                }
            }

            // The ground tiles and the palette exist.
            if (ArtLibrary.FindSheet("forest_ground") != null)
            {
                foreach (var path in GroundAutoTiles.TilePaths())
                    if (AssetDatabase.LoadAssetAtPath<TileBase>(path) == null)
                        problems.Add($"{Path.GetFileNameWithoutExtension(path)} tile is missing");
                if (AssetDatabase.LoadAssetAtPath<GameObject>(ForestGroundPalette.PalettePath) == null)
                    problems.Add("the Forest Ground tile palette is missing");
            }
            return problems;
        }
    }
}
