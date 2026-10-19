using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace BellwortBurrow.EditorTools
{
    /// <summary>
    /// Makes the ground tiles you paint with, in Assets/Art/Tiles/, from forest_ground.aseprite:
    /// - Forest Path and Forest Deer Trail: Unity Auto Tiles with a 3x3 mask. Paint where the path goes, any width
    ///   down to one tile, and Unity picks the piece for each tile from which of its eight neighbours are painted.
    ///   The edge sits a couple of pixels inside the painted tiles, so a path painted one tile wide is a narrow
    ///   footpath and every tile you paint shows.
    /// - Forest Grass: a Rule Tile that picks one of the grass variations at random.
    /// These are ordinary Unity tile assets. Open one in the Inspector to see or change which piece goes with which
    /// layout. Running this again refreshes them in place, so scenes painted with them keep working; a tile you changed
    /// by hand is left alone (see <see cref="GeneratedAssets"/>).
    ///
    /// Every ground set is painted together on the sheet as one patch, from <see cref="Template"/>: each painted ('#')
    /// cell of the slice is a piece, and its layout is read from its neighbours in the template, exactly as Unity
    /// reads them in a scene. The template holds all 47 layouts; a layout that comes up more than once gives Unity
    /// several pieces to pick from at random. tools/ground_sets.py reads this template too when it draws a set.
    /// </summary>
    public static class GroundAutoTiles
    {
        public const string Folder = ArtLibrary.ArtRoot + "/Tiles";
        const string SheetName = "forest_ground";

        /// <summary>
        /// The patch every 3x3 ground set is painted from, top row first: '#' is a painted tile, '.' is not. Outside
        /// it counts as not painted. Change it only together with the sheet's slices (and run ground_sets.py again).
        /// </summary>
        public static readonly string[] Template =
        {
            ".#..#.#.##.#.###.",
            "##.######.###.###",
            ".####.########.#.",
            "#########.##.####",
            ".#.####.######.#.",
            "####.#...#.##...#",
        };

        /// <summary>The layout every tile surrounded by painted tiles has (all nine bits).</summary>
        public const uint Full = 511;

        sealed class GroundSet
        {
            public string Asset, Slice;
            public string[] Fills; // extra full pieces, picked at random alongside the template's full piece
        }

        static readonly GroundSet[] Sets =
        {
            new GroundSet { Asset = "Forest Path", Slice = "path_set", Fills = new[] { "path_fill_00", "path_fill_01" } },
            new GroundSet { Asset = "Forest Deer Trail", Slice = "trail_set", Fills = new[] { "trail_00", "trail_01" } },
        };

        // Perlin noise favors the middle of the list, so the plainest grass goes in the middle.
        static readonly string[] GrassPieces = { "grass_00", "grass_01", "grass_02", "grass_03" };

        [MenuItem("Bellwort Burrow/Setup/Forest Steps/4. Make Ground Auto Tiles", false, 204)]
        static void MakeFromMenu()
        {
            if (!ArtLibrary.CheckArtInstalled()) return;
            Debug.Log($"Bellwort Burrow: made or refreshed {MakeAll()} ground tiles in {Folder}.");
        }

        /// <summary>Makes or refreshes every ground tile. Returns how many.</summary>
        public static int MakeAll()
        {
            string sheetPath = ArtLibrary.FindSheet(SheetName);
            if (sheetPath == null)
            {
                Debug.LogError($"Bellwort Burrow: couldn't find {SheetName}.aseprite under {ArtLibrary.ArtRoot}.");
                return 0;
            }
            var sprites = ArtLibrary.LoadSubAssets<Sprite>(sheetPath);
            ArtLibrary.EnsureFolder(Folder);

            var written = new List<string>();
            var kept = new List<string>();
            foreach (var set in Sets)
            {
                string path = $"{Folder}/{set.Asset}.asset";
                if (!GeneratedAssets.CanOverwrite(path)) kept.Add(path);
                else if (MakeSetTile(set, sprites, path)) written.Add(path);
            }
            string grassPath = $"{Folder}/Forest Grass.asset";
            if (!GeneratedAssets.CanOverwrite(grassPath)) kept.Add(grassPath);
            else if (MakeGrassTile(sprites, grassPath)) written.Add(grassPath);

            AssetDatabase.SaveAssets();
            foreach (var path in written) GeneratedAssets.Stamp(path);
            GeneratedAssets.ReportKept("Make Ground Auto Tiles", kept);
            return written.Count;
        }

        /// <summary>Paths in the order the painting tiles should sit in the tile palette.</summary>
        public static IEnumerable<string> TilePaths()
        {
            yield return $"{Folder}/Forest Grass.asset";
            foreach (var set in Sets) yield return $"{Folder}/{set.Asset}.asset";
        }

        /// <summary>
        /// The 3x3 layout of a painted template cell, as Unity's Auto Tile counts it (y up): top row 64 128 256,
        /// middle 8 16 32, bottom 1 2 4, with 16 the tile itself. A corner counts only when both sides next to it
        /// are painted, the same way Unity folds the 256 possible neighbourhoods down to 47 layouts.
        /// </summary>
        internal static uint Layout(string[] template, int column, int row)
        {
            bool Painted(int dx, int dy)
            {
                int x = column + dx, y = row + dy;
                return y >= 0 && y < template.Length && x >= 0 && x < template[y].Length && template[y][x] == '#';
            }
            bool n = Painted(0, -1), s = Painted(0, 1), w = Painted(-1, 0), e = Painted(1, 0);
            uint mask = 16;
            if (n) mask |= 128;
            if (s) mask |= 2;
            if (w) mask |= 8;
            if (e) mask |= 32;
            if (n && w && Painted(-1, -1)) mask |= 64;
            if (n && e && Painted(1, -1)) mask |= 256;
            if (s && w && Painted(-1, 1)) mask |= 1;
            if (s && e && Painted(1, 1)) mask |= 4;
            return mask;
        }

        /// <summary>Sprite name of a template cell: the importer numbers a slice's tiles row by row from the top left.</summary>
        internal static string PieceName(string slice, int column, int row, string[] template)
        {
            int columns = template[0].Length, count = columns * template.Length;
            string digits = new string('0', Mathf.Max(2, count.ToString().Length));
            return slice + "_" + (row * columns + column).ToString(digits);
        }

        static bool MakeSetTile(GroundSet set, Dictionary<string, Sprite> sprites, string path)
        {
            // Which pieces go with which layout, in template order.
            var masks = new List<uint>();
            var choices = new List<List<Sprite>>();
            int missing = 0;
            for (int row = 0; row < Template.Length; row++)
            {
                for (int column = 0; column < Template[row].Length; column++)
                {
                    if (Template[row][column] != '#') continue;
                    if (!sprites.TryGetValue(PieceName(set.Slice, column, row, Template), out var piece)) { missing++; continue; }
                    uint mask = Layout(Template, column, row);
                    int at = masks.IndexOf(mask);
                    if (at < 0) { masks.Add(mask); choices.Add(new List<Sprite>()); at = masks.Count - 1; }
                    choices[at].Add(piece);
                }
            }
            int full = masks.IndexOf(Full);
            if (full < 0)
            {
                Debug.LogWarning($"Bellwort Burrow: skipped {set.Asset}; {SheetName} has no {set.Slice} slice painted from the template.");
                return false;
            }
            foreach (var name in set.Fills)
                if (sprites.TryGetValue(name, out var fill)) choices[full].Add(fill);
            if (missing > 0)
                Debug.LogWarning($"Bellwort Burrow: {set.Asset}: {missing} painted cells of the template have no piece in {set.Slice}. " +
                                 "Is the slice 17 by 6 tiles and painted from GroundAutoTiles.Template?");
            if (masks.Count < 47)
                Debug.LogWarning($"Bellwort Burrow: {set.Asset} has {masks.Count} of the 47 layouts; tiles with a missing layout show the full piece.");

            var tile = AssetDatabase.LoadAssetAtPath<AutoTile>(path);
            bool isNew = tile == null;
            if (isNew) tile = ScriptableObject.CreateInstance<AutoTile>();

            tile.m_DefaultSprite = choices[full][0];
            tile.m_DefaultColliderType = Tile.ColliderType.None;
            tile.m_MaskType = AutoTile.AutoTileMaskType.Mask_3x3;
            tile.random = true;
            tile.m_TextureList = new List<Texture2D> { choices[full][0].texture };
            tile.m_TextureScaleList = new List<float> { 1f };
            if (isNew) AssetDatabase.CreateAsset(tile, path);

            // This is what clicking the masks in the Auto Tile inspector stores.
            var serialized = new SerializedObject(tile);
            var keys = serialized.FindProperty("m_AutoTileDictionary.keyData");
            var values = serialized.FindProperty("m_AutoTileDictionary.valueData");
            if (keys == null || values == null)
            {
                Debug.LogError($"Bellwort Burrow: couldn't fill in the layouts on {path}; the Auto Tile format may have changed. " +
                               "Set them by hand in its Inspector.");
                return false;
            }
            keys.arraySize = masks.Count;
            values.arraySize = masks.Count;
            for (int i = 0; i < masks.Count; i++)
            {
                keys.GetArrayElementAtIndex(i).uintValue = masks[i];
                var entry = values.GetArrayElementAtIndex(i);
                var spriteList = entry.FindPropertyRelative("spriteList");
                var textureList = entry.FindPropertyRelative("textureList");
                spriteList.arraySize = choices[i].Count;
                textureList.arraySize = choices[i].Count;
                for (int j = 0; j < choices[i].Count; j++)
                {
                    spriteList.GetArrayElementAtIndex(j).objectReferenceValue = choices[i][j];
                    textureList.GetArrayElementAtIndex(j).objectReferenceValue = choices[i][j].texture;
                }
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(tile);
            return true;
        }

        static bool MakeGrassTile(Dictionary<string, Sprite> sprites, string path)
        {
            var grass = new List<Sprite>();
            foreach (var name in GrassPieces)
                if (sprites.TryGetValue(name, out var sprite)) grass.Add(sprite);
            if (grass.Count == 0) return false;

            var tile = AssetDatabase.LoadAssetAtPath<RuleTile>(path);
            bool isNew = tile == null;
            if (isNew) tile = ScriptableObject.CreateInstance<RuleTile>();

            tile.m_DefaultSprite = grass[0];
            tile.m_DefaultColliderType = Tile.ColliderType.None;
            var rule = new RuleTile.TilingRule
            {
                m_Output = RuleTile.TilingRuleOutput.OutputSprite.Random,
                m_Sprites = grass.ToArray(),
                m_PerlinScale = 0.5f,
                m_ColliderType = Tile.ColliderType.None,
            };
            rule.m_Neighbors.Clear(); // no conditions: every grass tile picks a random variation
            tile.m_TilingRules = new List<RuleTile.TilingRule> { rule };

            if (isNew) AssetDatabase.CreateAsset(tile, path);
            EditorUtility.SetDirty(tile);
            return true;
        }
    }
}
