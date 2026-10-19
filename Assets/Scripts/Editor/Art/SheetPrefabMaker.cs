using System;
using System.Collections.Generic;
using System.IO;
using BellwortBurrow.Systems.World;
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
    /// - Where it meets the map: a <see cref="GridFootprint"/> with the base tiles it is rooted on, so it snaps to the
    ///   grid (see <see cref="Footprints"/>), or, for a tree house piece, a <see cref="TreeHouse"/> that puts it on a giant tree.
    /// - A backdrop (a sheet in Assets/Art/Backdrops) sits in a sorting group at <see cref="BackdropSortingOrder"/>, so in
    ///   every scene it draws behind the characters and props and in front of the ground tiles, with nothing to set per scene.
    /// Run it from Bellwort Burrow > Setup, or right-click sheets and choose Bellwort Burrow > Make Prefabs From Sheet.
    /// Running it again rebuilds the prefabs in place, so scenes keep their links. A prefab you changed by hand is left
    /// alone (see <see cref="GeneratedAssets"/>).
    /// </summary>
    public static class SheetPrefabMaker
    {
        const string AssetsMenuPath = "Assets/Bellwort Burrow/Make Prefabs From Sheet";

        /// <summary>Behind everything that stands in the world (order 0), in front of the ground tilemaps (-30 to -10).</summary>
        public const int BackdropSortingOrder = -5;
        const string BackdropFolder = ArtLibrary.ArtRoot + "/Backdrops/";

        [MenuItem(AssetsMenuPath)]
        static void MakeFromSelection()
        {
            int made = 0;
            var kept = new List<string>();
            foreach (var path in SelectedSheets())
                made += Make(path, kept);
            GeneratedAssets.ReportKept("Make Prefabs From Sheet", kept);
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

        /// <summary>Makes prefabs for every sprite sheet under Assets/Art. Returns how many. Prefabs changed by hand are left alone.</summary>
        public static int MakeAll()
        {
            int made = 0;
            var kept = new List<string>();
            foreach (var path in ArtLibrary.SheetPaths(tileSheets: false))
                made += Make(path, kept);
            GeneratedAssets.ReportKept("Make Prefabs From Sheets", kept);
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

        /// <summary>Makes a prefab per sprite on the sheet. Prefabs changed by hand are added to <paramref name="kept"/> instead.</summary>
        public static int Make(string sheetPath, List<string> kept)
        {
            var sprites = ArtLibrary.LoadSubAssets<Sprite>(sheetPath);
            bool backdrop = sheetPath.Replace('\\', '/').StartsWith(BackdropFolder);

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

                string prefabPath = $"{folder}/{pair.Key}.prefab";
                if (!GeneratedAssets.CanOverwrite(prefabPath)) { kept.Add(prefabPath); continue; }

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

                // Where it meets the map: whole base tiles it snaps to, or a tree house piece that goes on a giant tree.
                if (Footprints.IsTreeHousePiece(pair.Key, out var part, out var fit))
                {
                    var house = root.AddComponent<TreeHouse>();
                    house.part = part;
                    house.fit = fit;
                    // It sorts inside its tree's sorting group by plain renderer orders (see TreeHouse.SortingOrderFor).
                    var houseGroup = root.GetComponent<SortingGroup>();
                    if (houseGroup != null) Object.DestroyImmediate(houseGroup);
                    int order = TreeHouse.SortingOrderFor(part);
                    renderer.sortingOrder = order;
                    foreach (Transform child in root.transform)
                        if (child.TryGetComponent<SpriteRenderer>(out var childRenderer)) childRenderer.sortingOrder = order + 1;
                }
                else
                {
                    SheetSlice? footSlice = slices.TryGetValue(pair.Key, out var found) ? found : (SheetSlice?)null;
                    int tiles = Footprints.TilesWide(pair.Key, backdrop, sheet?.Art, sheet?.Width ?? 0, sheet?.Height ?? 0, footSlice);
                    if (tiles > 0) root.AddComponent<GridFootprint>().tilesWide = tiles;
                }

                if (backdrop)
                {
                    // A flat painting far behind the walkable band: it never sorts by height against what stands in front of it.
                    var group = root.GetComponent<SortingGroup>();
                    if (group == null) group = root.AddComponent<SortingGroup>();
                    group.sortingOrder = BackdropSortingOrder;
                }

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Object.DestroyImmediate(root);
                GeneratedAssets.Stamp(prefabPath);
                made++;
            }
            return made;
        }
    }
}
