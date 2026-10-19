using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace BellwortBurrow.EditorTools
{
    /// <summary>
    /// Makes the ground tiles you paint with, in Assets/Art/Tiles/, from forest_ground.aseprite:
    /// - Forest Path and Forest Deer Trail: Unity Rule Tiles that read all eight neighbours. Paint where the path goes,
    ///   any width down to one tile, and each tile picks its piece from which neighbours are painted. The edge sits a
    ///   couple of pixels inside the painted tiles, so every tile you paint shows. Paint a staircase and the steps
    ///   become a slope: a corner whose next step carries on diagonally is cut from tile corner to tile corner.
    /// - Forest Grass: a Rule Tile that picks one of the grass variations at random.
    /// These are ordinary Unity tile assets. Open one in the Inspector to see or change its rules. Running this again
    /// refreshes them in place, so scenes painted with them keep working; a tile you changed by hand is left alone
    /// (see <see cref="GeneratedAssets"/>).
    ///
    /// Every ground set is painted together on the sheet as one patch, from <see cref="Template"/>: each painted ('#')
    /// cell of the slice is a piece, and its layout is read from its neighbours in the template, exactly as the Rule
    /// Tile reads them in a scene. The template holds all 83 layouts (<see cref="Layout"/>); a layout that comes up
    /// more than once gives the tile several pieces to pick from at random. tools/ground_sets.py reads this template too.
    /// </summary>
    public static class GroundAutoTiles
    {
        public const string Folder = ArtLibrary.ArtRoot + "/Tiles";
        const string SheetName = "forest_ground";

        /// <summary>
        /// The patch every 3x3 ground set is painted from, top row first: '#' is a painted tile, '.' is not. Outside
        /// it counts as not painted. Change it only together with the sheet's slices.
        /// </summary>
        public static readonly string[] Template =
        {
            "##...##..#..##.##",
            "#...###.###.###.#",
            "####.#.##.######.",
            "...####.##.####.#",
            "#.##.#....#..###.",
            "#.#####...##..#.#",
            "...###.##..#.####",
            ".########.######.",
            "##.##.#.##.###...",
            "#.##.##.##..#..##",
        };

        /// <summary>How many layouts a ground set has: the 47 of Unity's 3x3 Auto Tile plus 36 slope pieces.</summary>
        public const int LayoutCount = 83;

        /// <summary>The layout every tile surrounded by painted tiles has (all nine bits).</summary>
        public const uint Full = 511;

        // Neighbour bits, y up: top row NW 64, N 128, NE 256; middle W 8, the tile 16, E 32; bottom SW 1, S 2, SE 4.
        const uint N = 128, S = 2, W = 8, E = 32, NW = 64, NE = 256, SW = 1, SE = 4;

        // The Rule Tile's neighbour order (its default positions), as bits.
        static readonly uint[] RuleOrder = { NW, N, NE, W, E, SW, S, SE };
        static readonly Vector3Int[] RulePositions =
        {
            new Vector3Int(-1, 1, 0), new Vector3Int(0, 1, 0), new Vector3Int(1, 1, 0),
            new Vector3Int(-1, 0, 0), new Vector3Int(1, 0, 0),
            new Vector3Int(-1, -1, 0), new Vector3Int(0, -1, 0), new Vector3Int(1, -1, 0),
        };

        sealed class GroundSet
        {
            public string Asset, Slice;
            public string[] Fills; // extra full pieces, picked at random alongside the template's full pieces
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
        /// Which corner neighbours change a tile's piece, given which sides are painted. A corner counts when both
        /// sides next to it are painted (an inner corner, as in Unity's Auto Tile), and also when it decides a slope:
        /// for an outer corner (both its sides empty), the neighbour past each end of that corner, along a painted
        /// side, says whether the edge carries on diagonally there.
        /// </summary>
        internal static uint RelevantCorners(bool n, bool s, bool w, bool e)
        {
            uint r = 0;
            if ((n && w) || (!n && !e && w) || (!s && !w && n)) r |= NW;
            if ((n && e) || (!n && !w && e) || (!s && !e && n)) r |= NE;
            if ((s && w) || (!s && !e && w) || (!n && !w && s)) r |= SW;
            if ((s && e) || (!s && !w && e) || (!n && !e && s)) r |= SE;
            return r;
        }

        /// <summary>
        /// A tile's layout from all eight neighbours (bits as above, 16 for the tile itself): the painted sides, plus
        /// the painted corners that matter (<see cref="RelevantCorners"/>). 83 layouts in all.
        /// </summary>
        internal static uint LayoutOf(uint neighbours)
        {
            bool n = (neighbours & N) != 0, s = (neighbours & S) != 0, w = (neighbours & W) != 0, e = (neighbours & E) != 0;
            uint layout = 16 | (neighbours & (N | S | W | E));
            return layout | (neighbours & RelevantCorners(n, s, w, e));
        }

        /// <summary>The layout of a painted template cell.</summary>
        internal static uint Layout(string[] template, int column, int row)
        {
            bool Painted(int dx, int dy)
            {
                int x = column + dx, y = row + dy;
                return y >= 0 && y < template.Length && x >= 0 && x < template[y].Length && template[y][x] == '#';
            }
            uint around = 16;
            if (Painted(0, -1)) around |= N;
            if (Painted(0, 1)) around |= S;
            if (Painted(-1, 0)) around |= W;
            if (Painted(1, 0)) around |= E;
            if (Painted(-1, -1)) around |= NW;
            if (Painted(1, -1)) around |= NE;
            if (Painted(-1, 1)) around |= SW;
            if (Painted(1, 1)) around |= SE;
            return LayoutOf(around);
        }

        /// <summary>
        /// The Rule Tile neighbour conditions for a layout, in the Rule Tile's order (NW, N, NE, W, E, SW, S, SE):
        /// This where the layout needs that neighbour painted, NotThis where it needs it empty, 0 where it doesn't care.
        /// </summary>
        internal static int[] RuleNeighbours(uint layout)
        {
            bool n = (layout & N) != 0, s = (layout & S) != 0, w = (layout & W) != 0, e = (layout & E) != 0;
            uint matters = N | S | W | E | RelevantCorners(n, s, w, e);
            var conditions = new int[8];
            for (int i = 0; i < 8; i++)
            {
                uint bit = RuleOrder[i];
                if ((matters & bit) == 0) conditions[i] = 0;
                else conditions[i] = (layout & bit) != 0 ? RuleTile.TilingRuleOutput.Neighbor.This : RuleTile.TilingRuleOutput.Neighbor.NotThis;
            }
            return conditions;
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
            var layouts = new List<uint>();
            var choices = new List<List<Sprite>>();
            int missing = 0;
            for (int row = 0; row < Template.Length; row++)
            {
                for (int column = 0; column < Template[row].Length; column++)
                {
                    if (Template[row][column] != '#') continue;
                    if (!sprites.TryGetValue(PieceName(set.Slice, column, row, Template), out var piece)) { missing++; continue; }
                    uint layout = Layout(Template, column, row);
                    int at = layouts.IndexOf(layout);
                    if (at < 0) { layouts.Add(layout); choices.Add(new List<Sprite>()); at = layouts.Count - 1; }
                    choices[at].Add(piece);
                }
            }
            int full = layouts.IndexOf(Full);
            if (full < 0)
            {
                Debug.LogWarning($"Bellwort Burrow: skipped {set.Asset}; {SheetName} has no {set.Slice} slice painted from the template.");
                return false;
            }
            foreach (var name in set.Fills)
                if (sprites.TryGetValue(name, out var fill)) choices[full].Add(fill);
            if (missing > 0)
                Debug.LogWarning($"Bellwort Burrow: {set.Asset}: {missing} painted cells of the template have no piece in {set.Slice}. " +
                                 $"Is the slice {Template[0].Length} by {Template.Length} tiles and painted from GroundAutoTiles.Template?");
            if (layouts.Count < LayoutCount)
                Debug.LogWarning($"Bellwort Burrow: {set.Asset} has {layouts.Count} of the {LayoutCount} layouts; tiles with a missing layout show the full piece.");

            var tile = ScriptableObject.CreateInstance<RuleTile>();
            tile.name = set.Asset;
            tile.m_DefaultSprite = choices[full][0];
            tile.m_DefaultColliderType = Tile.ColliderType.None;
            tile.m_TilingRules = new List<RuleTile.TilingRule>();
            for (int i = 0; i < layouts.Count; i++)
            {
                var rule = new RuleTile.TilingRule
                {
                    m_Neighbors = new List<int>(RuleNeighbours(layouts[i])),
                    m_NeighborPositions = new List<Vector3Int>(RulePositions),
                    m_Sprites = choices[i].ToArray(),
                    m_Output = choices[i].Count > 1 ? RuleTile.TilingRuleOutput.OutputSprite.Random : RuleTile.TilingRuleOutput.OutputSprite.Single,
                    m_PerlinScale = 0.5f,
                    m_ColliderType = Tile.ColliderType.None,
                    m_RuleTransform = RuleTile.TilingRuleOutput.Transform.Fixed,
                };
                tile.m_TilingRules.Add(rule);
            }
            SaveKeepingId(tile, path);
            return true;
        }

        /// <summary>
        /// Saves a tile at path. When a tile of another kind is already there (the Auto Tiles these used to be), its
        /// file is replaced but its .meta is kept, so the asset keeps its ID and every scene painted with it stays linked.
        /// </summary>
        static void SaveKeepingId(TileBase tile, string path)
        {
            var existing = AssetDatabase.LoadMainAssetAtPath(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(tile, path);
                return;
            }
            if (existing.GetType() == tile.GetType())
            {
                EditorUtility.CopySerialized(tile, existing);
                existing.name = Path.GetFileNameWithoutExtension(path);
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(tile);
                return;
            }
            string temp = $"{Path.GetDirectoryName(path).Replace('\\', '/')}/{Path.GetFileNameWithoutExtension(path)} (rebuilding).asset";
            AssetDatabase.CreateAsset(tile, temp);
            AssetDatabase.SaveAssets();
            File.Copy(temp, path, true);
            AssetDatabase.DeleteAsset(temp);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
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
