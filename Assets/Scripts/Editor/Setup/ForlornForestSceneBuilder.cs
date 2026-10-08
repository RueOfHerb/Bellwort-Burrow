using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace BellwortBurrow.EditorTools
{
    /// <summary>
    /// Builds Assets/Scenes/Zones/ForlornForest.unity: the forest hamlet proof of concept, with every forest asset placed,
    /// the ground painted, the tranquil blue zone light, window and wisp lights, and a 480x270 pixel perfect camera.
    /// No player, movement or interaction yet. Running it again rebuilds the scene from scratch, so make hand edits
    /// in a copy of the scene, or change the layout below.
    ///
    /// The layout is written in sketch pixels (16 per tile) with x to the right and y down, measured at each object's feet,
    /// the same way the concept render was drawn. The clearing is 70 x 48 tiles.
    /// </summary>
    public static class ForlornForestSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Zones/ForlornForest.unity";
        const string GroundSheet = "forest_ground";
        const string CharacterFolder = ArtLibrary.ArtRoot + "/Characters";

        const int Columns = 70, Rows = 48;
        const float TilePixels = 16f;
        const float TrailReach = 1.05f, PathReach = 1.2f;

        static readonly Color ZoneLightColor = new Color32(0xA4, 0xB8, 0xE6, 0xFF); // tranquil blue
        const float ZoneLightIntensity = 0.72f;
        static readonly Color NightColor = new Color32(34, 23, 25, 255); // forest floor, deepest shade

        const int GroundOrder = -30, PathOrder = -20, GroundDetailOrder = -10, AirOrder = 100;

        // ---------- Layout (sketch pixels, feet) ----------

        static readonly (string prefab, float x, float y)[] Trees =
        {
            ("giant_vine_home_0", 150, 488), ("giant_brown_home_0", 972, 480),
            ("vine_tree_1", 330, 252), ("brown_tree_2", 468, 216), ("vine_tree_0", 706, 196), ("brown_tree_1", 822, 214),
            ("web_between_trees_a", 600, 210), ("web_between_trees_b", 1060, 760), ("web_between_trees_c", 404, 268),
            ("young_brown", 459, 700), ("young_brown_b", 873, 700), ("young_brown_c", 236, 238), ("young_brown_c", 1004, 242),
            ("young_brown_with_web", 34, 760),
            ("young_vine", 296, 172), ("young_vine", 1108, 248), ("young_vine_b", 900, 262), ("young_vine_c", 58, 604),
            ("stump_brown", 520, 272), ("stump_vine", 700, 590),
        };

        static readonly (string prefab, float x, float y)[] Cottages =
        {
            ("hex_cottage_red", 404, 470), ("round_cottage_blue", 566, 444), ("hex_cottage_untinted", 726, 478),
            ("round_cottage_red", 152, 704), ("hex_cottage_blue", 352, 726), ("round_cottage_untinted", 560, 694),
            ("hex_cottage_brown", 772, 718), ("round_cottage_brown", 968, 702),
        };

        static readonly (string prefab, float x, float y)[] Nature =
        {
            ("edge_rock", 1094, 640), ("spider_web", 1058, 238),
            ("boulder", 610, 610), ("boulder_b", 1034, 520), ("boulder_c", 596, 104),
            ("boulder_silver", 246, 756), ("boulder_silver_b", 866, 760), ("boulder_silver_c", 300, 310),
            ("boulder_garnet", 664, 758), ("boulder_garnet_b", 520, 604), ("boulder_garnet_c", 930, 300),
            ("tall_grass_a", 44, 530), ("tall_grass_b", 60, 538), ("tall_grass_c", 296, 520), ("tall_grass_a", 306, 528),
            ("tall_grass_b", 486, 506), ("tall_grass_c", 646, 500), ("tall_grass_cut", 658, 508), ("tall_grass_a", 860, 500),
            ("tall_grass_b", 872, 508), ("tall_grass_c", 420, 120), ("tall_grass_a", 432, 128), ("tall_grass_b", 560, 276),
            ("tall_grass_cut", 770, 272), ("tall_grass_c", 1004, 600), ("tall_grass_a", 200, 756), ("tall_grass_b", 452, 756),
            ("giant_grass", 232, 330), ("giant_grass", 40, 420), ("giant_grass", 1062, 456),
            ("stone_embedded_a", 356, 546), ("stone_embedded_b", 474, 290), ("stone_embedded_c", 690, 540),
            ("stone_embedded_a", 912, 556), ("stone_embedded_b", 88, 570), ("stone_embedded_c", 606, 640),
            ("stone_embedded_a", 1000, 736), ("stone_embedded_b", 380, 752), ("stone_embedded_c", 200, 300),
        };

        static readonly (string prefab, float x, float y)[] Props = { ("signpost", 330, 548) };

        static readonly (string file, float x, float y)[] Characters = { ("white_cat", 612, 556), ("villager", 482, 560) };

        static readonly Vector2[] Wisps =
        {
            new Vector2(96, 60), new Vector2(420, 40), new Vector2(640, 150), new Vector2(900, 70), new Vector2(1060, 160),
            new Vector2(250, 380), new Vector2(840, 400), new Vector2(40, 640), new Vector2(700, 650), new Vector2(480, 330),
        };
        const int LanternFlyCount = 46;

        // Ground, in tiles (x right, y down), traced through the corners of the tile grid.
        static readonly Vector2[][] Trails =
        {
            Line(-1.5f, 17.4f, 6, 16.4f, 13, 17.6f, 20, 16.2f, 28, 17.0f, 36, 15.8f, 44, 16.8f, 52, 15.6f, 58, 16.4f, 71.5f, 16.0f),
            Line(36, 15.8f, 37, 8, 38.5f, -1.5f),
        };
        static readonly Vector2[] MainPath = Line(-1.5f, 35.6f, 8, 34.8f, 16, 35.8f, 24, 34.6f, 32, 35.4f, 40, 34.4f, 48, 35.6f, 56, 34.6f, 64, 35.2f, 71.5f, 34.8f);
        static readonly Vector2[] FrontPath = Line(-1.5f, 46.4f, 10, 46.0f, 20, 46.6f, 30, 46.0f, 40, 46.5f, 50, 46.0f, 60, 46.6f, 71.5f, 46.2f);
        static readonly float[] LanesBetweenPaths = { 15.5f, 41.4f };
        static readonly (string slice, int column, int row, int size)[] CropCircles = { ("crop_large", 33, 1, 6), ("crop_small", 13, 27, 4) };

        // ---------- Building ----------

        [MenuItem("Bellwort Burrow/Setup/Forest Steps/5. Build Forlorn Forest Scene", false, 205)]
        static void BuildFromMenu()
        {
            if (!ArtLibrary.CheckArtInstalled()) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        public static bool Build()
        {
            var prefabs = ArtLibrary.LoadPrefabs();
            if (prefabs.Count == 0)
            {
                Debug.LogError("Bellwort Burrow: no prefabs in Assets/Art/Prefabs yet. Run Set Up Forlorn Forest (All Steps) instead.");
                return false;
            }
            var missing = new HashSet<string>();
            var lit = ArtLibrary.LitMaterial;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            MakeCamera();
            MakeZoneLight();
            PaintGround(lit);

            var world = new GameObject("World").transform;
            var air = new GameObject("Air").transform;
            Place(prefabs, Trees, Group("Trees", world), missing);
            Place(prefabs, Cottages, Group("Cottages", world), missing);
            Place(prefabs, Nature, Group("Nature", world), missing);
            Place(prefabs, Props, Group("Props", world), missing);
            PlaceCharacters(Group("Characters", world), missing);

            var wisps = Group("Wisps", air);
            foreach (var spot in Wisps)
                Lift(Place(prefabs, "wisp", spot.x, spot.y, wisps, missing));

            var flies = Group("Lantern Flies", air);
            var random = new System.Random(7);
            for (int i = 0; i < LanternFlyCount; i++)
            {
                string kind = random.NextDouble() < 0.3 ? "lantern_fly_small" : "lantern_fly";
                float x = 8 + (float)random.NextDouble() * (Columns * TilePixels - 16);
                float y = 40 + (float)random.NextDouble() * (Rows * TilePixels - 60);
                Lift(Place(prefabs, kind, Mathf.Round(x), Mathf.Round(y), flies, missing));
            }

            foreach (var name in missing)
                Debug.LogWarning($"Bellwort Burrow: no prefab or art named \"{name}\", so it was left out of the scene.");

            ArtLibrary.EnsureFolder(Path.GetDirectoryName(ScenePath).Replace('\\', '/'));
            bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
            if (saved) Debug.Log($"Bellwort Burrow: built {ScenePath}.");
            return saved;
        }

        static Vector3 World(float x, float y) => new Vector3(x / TilePixels, (Rows * TilePixels - y) / TilePixels, 0f);

        static Transform Group(string name, Transform parent)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            return group;
        }

        static void MakeCamera()
        {
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            cameraObject.transform.position = new Vector3(Columns / 2f, 16.5f, -10f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 270f / 2f / ArtLibrary.PixelsPerUnit;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = NightColor;

            var pixelPerfect = cameraObject.AddComponent<PixelPerfectCamera>();
            pixelPerfect.assetsPPU = (int)ArtLibrary.PixelsPerUnit;
            pixelPerfect.refResolutionX = 480;
            pixelPerfect.refResolutionY = 270;
            pixelPerfect.gridSnapping = PixelPerfectCamera.GridSnapping.UpscaleRenderTexture;
            pixelPerfect.cropFrame = PixelPerfectCamera.CropFrame.None;
        }

        static void MakeZoneLight()
        {
            var lightObject = new GameObject("Zone Light (tranquil blue)");
            var light = lightObject.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.color = ZoneLightColor;
            light.intensity = ZoneLightIntensity;
            light.targetSortingLayers = GlowLights.AllSortingLayers();
        }

        // ---------- Ground ----------

        static void PaintGround(Material lit)
        {
            string sheetPath = ArtLibrary.FindSheet(GroundSheet);
            var tiles = sheetPath != null ? ArtLibrary.LoadSubAssets<TileBase>(sheetPath) : new Dictionary<string, TileBase>();
            if (tiles.Count == 0) Debug.LogWarning($"Bellwort Burrow: no ground tiles found in {GroundSheet}.aseprite.");

            var gridObject = new GameObject("Ground");
            gridObject.AddComponent<Grid>().cellSize = Vector3.one;
            var ground = MakeTilemap(gridObject.transform, "Grass and Deer Trails", GroundOrder, lit);
            var paths = MakeTilemap(gridObject.transform, "Paths", PathOrder, lit);
            var details = MakeTilemap(gridObject.transform, "Tufts and Crop Circles", GroundDetailOrder, lit);

            var trailCorners = Corners(Trails, TrailReach);
            var pathCorners = Corners(PathLines(), PathReach);

            for (int column = 0; column < Columns; column++)
            {
                for (int row = 0; row < Rows; row++)
                {
                    var cell = new Vector3Int(column, Rows - 1 - row, 0);
                    int hash = Hash(column, row);
                    int trail = DualGridMask(trailCorners, column, row);
                    int path = DualGridMask(pathCorners, column, row);

                    string groundTile =
                        trail == 0 ? $"grass_{hash % 4:00}" :
                        trail == 15 ? (hash % 3 == 0 ? "trail_edge_15" : $"trail_{hash % 2:00}") :
                        $"trail_edge_{trail:00}";
                    SetTile(ground, cell, tiles, groundTile);

                    if (path == 15) SetTile(paths, cell, tiles, $"path_fill_{hash % 2:00}");
                    else if (path != 0) SetTile(paths, cell, tiles, $"path_edge_{path:00}");

                    if (trail == 0 && path == 0 && hash % 100 < 6)
                        SetTile(details, cell, tiles, $"tuft_{(hash / 100) % 2:00}");
                }
            }

            foreach (var circle in CropCircles)
            {
                for (int row = 0; row < circle.size; row++)
                {
                    for (int column = 0; column < circle.size; column++)
                    {
                        var cell = new Vector3Int(circle.column + column, Rows - 1 - (circle.row + row), 0);
                        string name = $"{circle.slice}_{row * circle.size + column:00}";
                        if (tiles.TryGetValue(name, out var tile)) details.SetTile(cell, tile);
                    }
                }
            }

            ground.CompressBounds();
            paths.CompressBounds();
            details.CompressBounds();
        }

        static Tilemap MakeTilemap(Transform grid, string name, int order, Material material)
        {
            var layer = new GameObject(name);
            layer.transform.SetParent(grid, false);
            var tilemap = layer.AddComponent<Tilemap>();
            var renderer = layer.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = order;
            if (material != null) renderer.sharedMaterial = material;
            return tilemap;
        }

        static void SetTile(Tilemap tilemap, Vector3Int cell, Dictionary<string, TileBase> tiles, string name)
        {
            if (tiles.TryGetValue(name, out var tile)) tilemap.SetTile(cell, tile);
        }

        static List<Vector2[]> PathLines()
        {
            var lines = new List<Vector2[]> { MainPath, FrontPath };

            // A short path from every front door down to the nearest path below it.
            var homes = new List<(string prefab, float x, float y)>(Cottages) { Trees[0], Trees[1] };
            foreach (var home in homes)
            {
                float x = home.x / TilePixels, feet = home.y / TilePixels;
                float main = HeightAt(MainPath, x), front = HeightAt(FrontPath, x);
                float target = feet < main ? main : front;
                lines.Add(Line(x, feet, x, target));
            }
            foreach (float x in LanesBetweenPaths)
                lines.Add(Line(x, HeightAt(MainPath, x), x, HeightAt(FrontPath, x)));

            // The lane that climbs from the main path up to the deer trail.
            lines.Add(Line(19.4f, HeightAt(MainPath, 19.4f), 19.6f, 24f, 18.8f, 17f));
            return lines;
        }

        /// <summary>Which tile corners lie close to any of the lines. Corner (i, j) is the top left corner of tile (i, j).</summary>
        static bool[,] Corners(IEnumerable<Vector2[]> lines, float reach)
        {
            var corners = new bool[Columns + 1, Rows + 1];
            for (int i = 0; i <= Columns; i++)
            {
                for (int j = 0; j <= Rows; j++)
                {
                    var corner = new Vector2(i, j);
                    foreach (var line in lines)
                    {
                        if (DistanceToLine(corner, line) < reach) { corners[i, j] = true; break; }
                    }
                }
            }
            return corners;
        }

        /// <summary>Dual grid tile number: top left 1, top right 2, bottom left 4, bottom right 8.</summary>
        public static int DualGridMask(bool[,] corners, int column, int row) =>
            (corners[column, row] ? 1 : 0) |
            (corners[column + 1, row] ? 2 : 0) |
            (corners[column, row + 1] ? 4 : 0) |
            (corners[column + 1, row + 1] ? 8 : 0);

        static float DistanceToLine(Vector2 point, Vector2[] line)
        {
            float best = float.MaxValue;
            for (int i = 0; i + 1 < line.Length; i++)
            {
                Vector2 a = line[i], b = line[i + 1], ab = b - a;
                float t = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(point - a, ab) / ab.sqrMagnitude) : 0f;
                best = Mathf.Min(best, Vector2.Distance(point, a + ab * t));
            }
            return best;
        }

        static float HeightAt(Vector2[] line, float x)
        {
            for (int i = 0; i + 1 < line.Length; i++)
            {
                if (x < line[i].x || x > line[i + 1].x) continue;
                float t = Mathf.InverseLerp(line[i].x, line[i + 1].x, x);
                return Mathf.Lerp(line[i].y, line[i + 1].y, t);
            }
            return x < line[0].x ? line[0].y : line[line.Length - 1].y;
        }

        static Vector2[] Line(params float[] xy)
        {
            var points = new Vector2[xy.Length / 2];
            for (int i = 0; i < points.Length; i++) points[i] = new Vector2(xy[i * 2], xy[i * 2 + 1]);
            return points;
        }

        static int Hash(int column, int row)
        {
            unchecked
            {
                uint h = (uint)(column * 73856093) ^ (uint)(row * 19349663);
                h ^= h >> 13; h *= 0x5bd1e995u; h ^= h >> 15;
                return (int)(h & 0x7fffffff);
            }
        }

        // ---------- Objects ----------

        static void Place(Dictionary<string, GameObject> prefabs, (string prefab, float x, float y)[] layout, Transform parent, HashSet<string> missing)
        {
            foreach (var item in layout) Place(prefabs, item.prefab, item.x, item.y, parent, missing);
        }

        static GameObject Place(Dictionary<string, GameObject> prefabs, string name, float x, float y, Transform parent, HashSet<string> missing)
        {
            if (!prefabs.TryGetValue(name, out var prefab))
            {
                missing.Add(name);
                return null;
            }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.position = World(x, y);
            return instance;
        }

        /// <summary>Wisps and lantern flies float, so they draw over everything on the ground.</summary>
        static void Lift(GameObject flier)
        {
            if (flier == null) return;
            if (flier.TryGetComponent<SortingGroup>(out var group)) group.sortingOrder = AirOrder;
            else if (flier.TryGetComponent<SpriteRenderer>(out var renderer)) renderer.sortingOrder = AirOrder;
        }

        static void PlaceCharacters(Transform parent, HashSet<string> missing)
        {
            foreach (var character in Characters)
            {
                string path = $"{CharacterFolder}/{character.file}.aseprite";
                GameObject instance = null;
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model != null)
                {
                    instance = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
                }
                else
                {
                    var sprites = ArtLibrary.LoadSubAssets<Sprite>(path);
                    foreach (var sprite in sprites.Values)
                    {
                        instance = new GameObject(character.file);
                        instance.transform.SetParent(parent, false);
                        var renderer = instance.AddComponent<SpriteRenderer>();
                        renderer.sprite = sprite;
                        renderer.sharedMaterial = ArtLibrary.LitMaterial;
                        break;
                    }
                }
                if (instance == null) { missing.Add(character.file); continue; }

                instance.transform.position = World(character.x, character.y);
                foreach (var renderer in instance.GetComponentsInChildren<SpriteRenderer>())
                    renderer.spriteSortPoint = SpriteSortPoint.Pivot;
            }
        }
    }
}
