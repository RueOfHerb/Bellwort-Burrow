using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace BellwortBurrow.EditorTools
{
    /// <summary>
    /// Makes the ground tiles you paint with, in Assets/Art/Tiles/, from forest_ground.aseprite:
    /// - Forest Path and Forest Deer Trail: Unity Auto Tiles with a 2x2 mask. Paint where the path goes (at least two
    ///   tiles wide) and Unity picks the edge pieces; the edges fall halfway into the outer tiles, so three painted rows
    ///   make a path two tiles wide.
    /// - Forest Grass: a Rule Tile that picks one of the grass variations at random.
    /// These are ordinary Unity tile assets. Open one in the Inspector to see or change which piece goes with which
    /// layout. Running this again refreshes them in place, so scenes painted with them keep working; a tile you changed
    /// by hand is left alone (see <see cref="GeneratedAssets"/>).
    ///
    /// A 2x2 sheet slice holds 16 pieces named "slice_00" to "slice_15", numbered by which corners of the piece are
    /// filled: top left 1, top right 2, bottom left 4, bottom right 8 (00 is empty, 15 is full).
    /// </summary>
    public static class GroundAutoTiles
    {
        public const string Folder = ArtLibrary.ArtRoot + "/Tiles";
        const string SheetName = "forest_ground";

        sealed class CornerSet
        {
            public string Asset, Pieces;
            public string[] Fills; // extra full pieces, picked at random with piece 15
        }

        static readonly CornerSet[] CornerSets =
        {
            new CornerSet { Asset = "Forest Path", Pieces = "path_edge", Fills = new[] { "path_fill_00", "path_fill_01" } },
            new CornerSet { Asset = "Forest Deer Trail", Pieces = "trail_edge", Fills = new[] { "trail_00", "trail_01" } },
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
            foreach (var set in CornerSets)
            {
                string path = $"{Folder}/{set.Asset}.asset";
                if (!GeneratedAssets.CanOverwrite(path)) kept.Add(path);
                else if (MakeCornerTile(set, sprites, path)) written.Add(path);
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
            foreach (var set in CornerSets) yield return $"{Folder}/{set.Asset}.asset";
        }

        static bool MakeCornerTile(CornerSet set, Dictionary<string, Sprite> sprites, string path)
        {
            var fills = new List<Sprite>();
            foreach (var name in set.Fills)
                if (sprites.TryGetValue(name, out var fill)) fills.Add(fill);
            if (sprites.TryGetValue($"{set.Pieces}_15", out var full)) fills.Insert(0, full);
            if (fills.Count == 0)
            {
                Debug.LogWarning($"Bellwort Burrow: skipped {set.Asset}; {SheetName} has no {set.Pieces}_15 or fill pieces.");
                return false;
            }

            var tile = AssetDatabase.LoadAssetAtPath<AutoTile>(path);
            bool isNew = tile == null;
            if (isNew) tile = ScriptableObject.CreateInstance<AutoTile>();

            tile.m_DefaultSprite = fills[0];
            tile.m_DefaultColliderType = Tile.ColliderType.None;
            tile.m_MaskType = AutoTile.AutoTileMaskType.Mask_2x2;
            tile.random = true;
            tile.m_TextureList = new List<Texture2D> { fills[0].texture };
            tile.m_TextureScaleList = new List<float> { 1f };
            if (isNew) AssetDatabase.CreateAsset(tile, path);

            // Which pieces go with which layout. This is what clicking the masks in the Auto Tile inspector stores.
            var masks = new List<uint>();
            var choices = new List<List<Sprite>>();
            for (int corners = 1; corners <= 15; corners++)
            {
                var pieces = new List<Sprite>();
                if (corners == 15) pieces.AddRange(fills);
                else if (sprites.TryGetValue($"{set.Pieces}_{corners:00}", out var piece)) pieces.Add(piece);
                if (pieces.Count == 0) continue;
                masks.Add(ToAutoTileMask(corners));
                choices.Add(pieces);
            }

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

        /// <summary>
        /// Sheet corners (top left 1, top right 2, bottom left 4, bottom right 8) to the Auto Tile's 2x2 mask
        /// (bottom left 1, bottom right 2, top left 4, top right 8).
        /// </summary>
        internal static uint ToAutoTileMask(int corners)
        {
            uint mask = 0;
            if ((corners & 1) != 0) mask |= 4;
            if ((corners & 2) != 0) mask |= 8;
            if ((corners & 4) != 0) mask |= 1;
            if ((corners & 8) != 0) mask |= 2;
            return mask;
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
