using System.Collections.Generic;
using BellwortBurrow.Systems.World;
using BellwortBurrow.Tiles;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace BellwortBurrow.EditorTools
{
    /// <summary>
    /// Works out a zone's ground heights from its Banks layer for the Tree Shadow Clock (lvl-5): Bellwort Burrow > Tiles >
    /// Work Out Heights, and again on its own a moment after the Banks layer is painted, in any scene whose clock already
    /// has heights. Reads where the faces are and, from each face tile's own pixels (the earth or stone, not the grass it carries), where its foot and lip are.
    /// </summary>
    [InitializeOnLoad]
    public static class TerrainHeightsBaker
    {
        const string Folder = "Assets/Data/Heights";
        const double Settle = 1.0;   // seconds after the last painted tile

        static readonly HashSet<Scene> waiting = new();
        static double lastChange;

        static TerrainHeightsBaker()
        {
            Tilemap.tilemapTileChanged += OnTilesChanged;
            EditorApplication.update += OnUpdate;
        }

        [MenuItem("Bellwort Burrow/Tiles/Work Out Heights")]
        static void WorkOutHeights()
        {
            int count = 0;
            foreach (var clock in Object.FindObjectsByType<TreeShadowClock>(FindObjectsInactive.Include))
                if (Bake(clock, true)) count++;
            Debug.Log(count == 0
                ? "Work Out Heights: no Tree Shadow Clock with a ground tilemap in the open scenes."
                : $"Work Out Heights: worked out the ground's heights for {count} zone(s).");
        }

        static void OnTilesChanged(Tilemap map, Tilemap.SyncTile[] tiles)
        {
            if (Application.isPlaying || map == null || map.name != "Banks") return;
            waiting.Add(map.gameObject.scene);
            lastChange = EditorApplication.timeSinceStartup;
        }

        static void OnUpdate()
        {
            if (waiting.Count == 0 || EditorApplication.timeSinceStartup - lastChange < Settle) return;
            var scenes = new List<Scene>(waiting);
            waiting.Clear();
            foreach (var clock in Object.FindObjectsByType<TreeShadowClock>(FindObjectsInactive.Include))
            {
                if (!scenes.Contains(clock.gameObject.scene)) continue;
                var so = new SerializedObject(clock);
                if (so.FindProperty("heights").objectReferenceValue != null) Bake(clock, false);
            }
        }

        /// <summary>Works out the heights under a clock and saves them to its heights asset (made if it has none).</summary>
        public static bool Bake(TreeShadowClock clock, bool makeAsset)
        {
            var so = new SerializedObject(clock);
            var area = so.FindProperty("area").objectReferenceValue as Tilemap;
            var heightsProp = so.FindProperty("heights");
            int tilePixels = so.FindProperty("pixelsPerUnit").intValue;
            if (area == null) return false;
            var heights = heightsProp.objectReferenceValue as TerrainHeights;
            if (heights == null && !makeAsset) return false;

            var banks = new List<Tilemap>();
            foreach (var root in clock.gameObject.scene.GetRootGameObjects())
                foreach (var map in root.GetComponentsInChildren<Tilemap>(true))
                    if (map.name == "Banks") banks.Add(map);

            var bounds = area.cellBounds;
            int w = bounds.size.x, h = bounds.size.y;
            var face = new bool[w, h];
            var faceMap = new Tilemap[w, h];
            foreach (var map in banks)
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var cell = new Vector3Int(bounds.xMin + x, bounds.yMin + y, 0);
                    if (map.GetTile(cell) is FaceTile) { face[x, y] = true; faceMap[x, y] = map; }
                }

            bool same = heights != null && heights.width == w && heights.height == h &&
                        heights.origin == new Vector2Int(bounds.xMin, bounds.yMin) && heights.cells.Length == w * h;
            var cells = TerrainHeights.Solve(face, same ? heights.cells : null);
            var art = new Dictionary<Texture2D, Color32[]>();
            var fx = new List<int>(); var fb = new List<int>(); var ft = new List<int>();
            var lo = new List<float>(); var hi = new List<float>(); var edges = new List<byte>();
            for (int x = 0; x < w; x++)
            {
                int y = 0;
                while (y < h)
                {
                    if (!face[x, y]) { y++; continue; }
                    int bottom = y;
                    while (y < h && face[x, y]) y++;
                    float below = bottom > 0 ? cells[(bottom - 1) * w + x] : float.NaN;
                    float above = y < h ? cells[y * w + x] : float.NaN;
                    if (float.IsNaN(below)) below = float.IsNaN(above) ? 0f : above - 1f;
                    if (float.IsNaN(above)) above = below + 1f;
                    fx.Add(x); fb.Add(bottom); ft.Add(y); lo.Add(below); hi.Add(Mathf.Max(below, above));
                    for (int i = 0; i < tilePixels; i++)
                    {
                        int foot = -1, lip = -1;
                        for (int r = bottom; r < y; r++)
                        {
                            var cell = new Vector3Int(bounds.xMin + x, bounds.yMin + r, 0);
                            var map = faceMap[x, r];
                            var sprite = map.GetSprite(cell);
                            if (sprite == null) continue;
                            bool flipped = map.GetTransformMatrix(cell).m00 < 0f;
                            for (int py = 0; py < tilePixels; py++)
                            {
                                if (!IsFace(sprite, flipped ? tilePixels - 1 - i : i, py, art)) continue;
                                int up = (r - bottom) * tilePixels + py;
                                if (foot < 0) foot = up;
                                lip = up + 1;
                            }
                        }
                        edges.Add(foot < 0 ? TerrainHeights.NoFace : (byte)Mathf.Min(foot, 254));
                        edges.Add(lip < 0 ? TerrainHeights.NoFace : (byte)Mathf.Min(lip, 254));
                    }
                }
            }

            if (heights == null)
            {
                if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Data", "Heights");
                heights = ScriptableObject.CreateInstance<TerrainHeights>();
                AssetDatabase.CreateAsset(heights, AssetDatabase.GenerateUniqueAssetPath($"{Folder}/{clock.gameObject.scene.name} Heights.asset"));
                heightsProp.objectReferenceValue = heights;
                so.ApplyModifiedProperties();
                EditorSceneManager.MarkSceneDirty(clock.gameObject.scene);
            }

            Undo.RecordObject(heights, "Work Out Heights");
            heights.origin = new Vector2Int(bounds.xMin, bounds.yMin);
            heights.width = w;
            heights.height = h;
            heights.tilePixels = tilePixels;
            heights.cells = cells;
            heights.faceX = fx.ToArray(); heights.faceBottom = fb.ToArray(); heights.faceTop = ft.ToArray();
            heights.faceLow = lo.ToArray(); heights.faceHigh = hi.ToArray(); heights.faceEdges = edges.ToArray();
            heights.version++;
            EditorUtility.SetDirty(heights);
            AssetDatabase.SaveAssetIfDirty(heights);
            clock.ReloadHeights();
            return true;
        }

        // Whether a pixel of a face tile's sprite (x, y from its lower left) is the face itself: drawn, and not the grass
        // the tile carries at its lip and foot (grass is the only green-leaning colour on a face's sheet; earth and stone
        // lean red or blue).
        static bool IsFace(Sprite sprite, int x, int y, Dictionary<Texture2D, Color32[]> art)
        {
            var tex = sprite.texture;
            if (!art.TryGetValue(tex, out var px))
            {
                px = tex.isReadable ? tex.GetPixels32() : null;
                art[tex] = px;
            }
            if (px == null) return true;   // unreadable: the whole cell counts as face
            var r = sprite.textureRect;
            int tx = Mathf.RoundToInt(r.x) + x, ty = Mathf.RoundToInt(r.y) + y;
            if (tx < 0 || ty < 0 || tx >= tex.width || ty >= tex.height || x >= r.width || y >= r.height) return false;
            var c = px[ty * tex.width + tx];
            return c.a > 0 && !(c.g > c.r && c.g >= c.b);
        }
    }
}
