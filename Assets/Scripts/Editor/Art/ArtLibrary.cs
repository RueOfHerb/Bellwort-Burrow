using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BellwortBurrow.EditorTools
{
    /// <summary>
    /// Where the art lives, plus the small helpers every art tool shares.
    /// Everything under Assets/Art comes from the art package (see the README at the repo root), not from git.
    /// </summary>
    public static class ArtLibrary
    {
        public const string ArtRoot = "Assets/Art";
        public const string PrefabRoot = ArtRoot + "/Prefabs";
        public const string CharacterFolder = ArtRoot + "/Characters";
        public const float PixelsPerUnit = 16f;

        const string LitMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat";
        const string UnlitMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

        /// <summary>Lit by the zone light and point lights.</summary>
        public static Material LitMaterial => AssetDatabase.LoadAssetAtPath<Material>(LitMaterialPath);

        /// <summary>Ignores lighting, so glows stay bright at night.</summary>
        public static Material UnlitMaterial => AssetDatabase.LoadAssetAtPath<Material>(UnlitMaterialPath);

        /// <summary>Returns false, and says how to get the art, when Assets/Art is missing.</summary>
        public static bool CheckArtInstalled()
        {
            if (AssetDatabase.IsValidFolder(ArtRoot)) return true;
            Debug.LogError("Bellwort Burrow: Assets/Art is missing. Download the art package, then choose " +
                           "Bellwort Burrow > Art Package > Install Art Package (see README.md).");
            return false;
        }

        /// <summary>
        /// Makes every Aseprite file under Assets/Art use the Bellwort Sheet Importer, except characters, which keep
        /// Unity's Aseprite importer for their animations. Sheets named "..._ground" are cut into 16 px tiles.
        /// Unity falls back to its own importer when a sheet arrives before this code has compiled, so this puts it back.
        /// Returns how many sheets were switched.
        /// </summary>
        public static int UseSheetImporter()
        {
            int switched = 0;
            foreach (var path in AssetDatabase.GetAllAssetPaths())
            {
                if (!path.StartsWith(ArtRoot + "/") || path.StartsWith(CharacterFolder + "/")) continue;
                if (!path.EndsWith(".aseprite") && !path.EndsWith(".ase")) continue;
                if (AssetImporter.GetAtPath(path) is BellwortSheetImporter) continue;

                AssetDatabase.SetImporterOverride<BellwortSheetImporter>(path);
                if (AssetImporter.GetAtPath(path) is BellwortSheetImporter importer)
                {
                    importer.pixelsPerUnit = PixelsPerUnit;
                    importer.tileSize = Path.GetFileNameWithoutExtension(path).EndsWith("_ground") ? 16 : 0;
                    // SaveAndReimport only saves settings on an importer marked dirty.
                    EditorUtility.SetDirty(importer);
                    importer.SaveAndReimport();
                    switched++;
                }
                else
                {
                    Debug.LogWarning($"Bellwort Burrow: couldn't switch {path} to the Bellwort Sheet Importer.");
                }
            }
            return switched;
        }

        /// <summary>Sheets under Assets/Art that use the Bellwort Sheet Importer: sprite sheets, or tile sheets.</summary>
        public static List<string> SheetPaths(bool tileSheets)
        {
            var paths = new List<string>();
            foreach (var path in AssetDatabase.GetAllAssetPaths())
            {
                if (!path.StartsWith(ArtRoot + "/")) continue;
                if (!path.EndsWith(".aseprite") && !path.EndsWith(".ase")) continue;
                if (AssetImporter.GetAtPath(path) is BellwortSheetImporter importer && (importer.tileSize > 0) == tileSheets)
                    paths.Add(path);
            }
            paths.Sort(System.StringComparer.Ordinal);
            return paths;
        }

        /// <summary>Finds a sheet by file name, for example "forest_ground". Null when it isn't installed.</summary>
        public static string FindSheet(string sheetName)
        {
            foreach (var path in SheetPaths(false))
                if (Path.GetFileNameWithoutExtension(path) == sheetName) return path;
            foreach (var path in SheetPaths(true))
                if (Path.GetFileNameWithoutExtension(path) == sheetName) return path;
            return null;
        }

        /// <summary>Sub-assets of one type in an imported file, by name (first one wins).</summary>
        public static Dictionary<string, T> LoadSubAssets<T>(string path) where T : Object
        {
            var result = new Dictionary<string, T>();
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is T typed && !result.ContainsKey(typed.name))
                    result[typed.name] = typed;
            return result;
        }

        /// <summary>Every prefab under Assets/Art/Prefabs, by file name.</summary>
        public static Dictionary<string, GameObject> LoadPrefabs()
        {
            var result = new Dictionary<string, GameObject>();
            if (!AssetDatabase.IsValidFolder(PrefabRoot)) return result;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null) result[Path.GetFileNameWithoutExtension(path)] = prefab;
            }
            return result;
        }

        public static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
