using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace BellwortBurrow.EditorTools
{
    /// <summary>
    /// Makes one prefab per sprite on a sheet, in Assets/Art/Prefabs/&lt;sheet&gt;/.
    /// - The sprite sits on a lit material, with its feet on the prefab's origin.
    /// - A matching "_glow" sprite becomes a Glow child on an unlit material, so windows, wisps and gem glints stay bright at night.
    /// - Bright spots on the glow become point lights (see <see cref="GlowLights"/>).
    /// Run it from Bellwort Burrow > Setup, or right-click sheets and choose Bellwort Burrow > Make Prefabs From Sheet.
    /// Running it again rebuilds the prefabs in place, so scenes keep their links. Add your own changes in the scene
    /// or in a prefab variant, since a rebuild replaces edits made inside these prefabs.
    /// </summary>
    public static class SheetPrefabMaker
    {
        const string AssetsMenuPath = "Assets/Bellwort Burrow/Make Prefabs From Sheet";

        [MenuItem(AssetsMenuPath)]
        static void MakeFromSelection()
        {
            int made = 0;
            foreach (var path in SelectedSheets())
                made += Make(path);
            AssetDatabase.SaveAssets();
            Debug.Log($"Bellwort Burrow: made or updated {made} prefabs in {ArtLibrary.PrefabRoot}.");
        }

        [MenuItem(AssetsMenuPath, true)]
        static bool CanMake()
        {
            foreach (var _ in SelectedSheets()) return true;
            return false;
        }

        [MenuItem("Bellwort Burrow/Setup/Forest Steps/2. Make Prefabs From Sheets", false, 202)]
        static void MakeAllFromMenu()
        {
            if (!ArtLibrary.CheckArtInstalled()) return;
            int made = MakeAll();
            Debug.Log($"Bellwort Burrow: made or updated {made} prefabs in {ArtLibrary.PrefabRoot}.");
        }

        /// <summary>Makes prefabs for every sprite sheet under Assets/Art. Returns how many.</summary>
        public static int MakeAll()
        {
            int made = 0;
            foreach (var path in ArtLibrary.SheetPaths(tileSheets: false))
                made += Make(path);
            AssetDatabase.SaveAssets();
            return made;
        }

        static IEnumerable<string> SelectedSheets()
        {
            foreach (var guid in Selection.assetGUIDs)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is BellwortSheetImporter importer && importer.tileSize <= 0)
                    yield return path;
            }
        }

        public static int Make(string sheetPath)
        {
            var sprites = ArtLibrary.LoadSubAssets<Sprite>(sheetPath);

            // The sheet itself tells us where the glow sits, for the lights.
            AsepriteSheet sheet = null;
            var slices = new Dictionary<string, SheetSlice>();
            try
            {
                sheet = AsepriteSheet.Read(File.ReadAllBytes(sheetPath));
                foreach (var slice in sheet.Slices)
                    if (!slices.ContainsKey(slice.Name)) slices[slice.Name] = slice;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Bellwort Burrow: could not read {sheetPath} for lights ({e.Message}). Prefabs are made without lights.");
            }

            string folder = $"{ArtLibrary.PrefabRoot}/{Path.GetFileNameWithoutExtension(sheetPath)}";
            ArtLibrary.EnsureFolder(folder);
            var lit = ArtLibrary.LitMaterial;
            var unlit = ArtLibrary.UnlitMaterial;

            int made = 0;
            foreach (var pair in sprites)
            {
                if (pair.Key.EndsWith("_glow") && sprites.ContainsKey(pair.Key.Substring(0, pair.Key.Length - 5))) continue;

                var root = new GameObject(pair.Key);
                var renderer = root.AddComponent<SpriteRenderer>();
                renderer.sprite = pair.Value;
                // Sort by the feet (the pivot), not the middle of the picture.
                renderer.spriteSortPoint = SpriteSortPoint.Pivot;
                if (lit != null) renderer.sharedMaterial = lit;

                if (sprites.TryGetValue(pair.Key + "_glow", out var glowSprite))
                {
                    var glow = new GameObject("Glow");
                    glow.transform.SetParent(root.transform, false);
                    var glowRenderer = glow.AddComponent<SpriteRenderer>();
                    glowRenderer.sprite = glowSprite;
                    glowRenderer.spriteSortPoint = SpriteSortPoint.Pivot;
                    glowRenderer.sortingOrder = 1;
                    if (unlit != null) glowRenderer.sharedMaterial = unlit;
                    // Keeps the glow drawn with its art when sprites sort by height.
                    root.AddComponent<SortingGroup>();

                    if (sheet != null && slices.TryGetValue(pair.Key, out var slice))
                        GlowLights.AddLights(root, GlowLights.Find(sheet, slice));
                }

                PrefabUtility.SaveAsPrefabAsset(root, $"{folder}/{pair.Key}.prefab");
                Object.DestroyImmediate(root);
                made++;
            }
            return made;
        }
    }
}
