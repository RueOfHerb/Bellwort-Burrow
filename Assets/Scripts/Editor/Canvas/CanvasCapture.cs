using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;

namespace BellwortBurrow.EditorTools
{
    /// <summary>
    /// Bellwort Burrow > Canvas > Capture Shots: saves the images for the Bellwort Biome Palettes canvas into Captures/
    /// (next to Assets, ignored by git), straight from Unity and the sheets, so the canvas never shows redrawn art.
    /// - Scene shots of the Forlorn Forest, rendered by a camera to a texture with Unity's own lighting and sorting:
    ///   the opening screen (what the Main Camera sees), the whole hamlet, and two screens stacked above each tree home.
    /// - Every sheet and character exactly as drawn: the Art layer, pixel for pixel.
    /// - captures.json: when, and the fingerprint of every source file the images came from, for the Sync Ledger.
    /// - art-rules.txt: what Check Art Rules found.
    /// Images are saved at 1 pixel per art pixel; the canvas scales them up with pixelated rendering.
    /// It opens the forest scene to render it and puts your open scenes back afterwards. It never saves a scene.
    /// </summary>
    public static class CanvasCapture
    {
        public const string Folder = "Captures";
        const string ScenePath = "Assets/Scenes/Zones/ForlornForest.unity";
        const int ScreenWidth = 480, ScreenHeight = 270;
        const int FeetAboveBottom = 60; // tree home frames: pixels of ground below the home's feet
        static readonly Color NightColor = new Color32(34, 23, 25, 255);

        [MenuItem("Bellwort Burrow/Canvas/Capture Shots", false, 400)]
        static void CaptureFromMenu()
        {
            if (Capture(out int count))
                EditorUtility.DisplayDialog("Bellwort Burrow", $"Saved {count} canvas images in {Path.GetFullPath(Folder)}.", "OK");
        }

        public static bool Capture(out int count)
        {
            count = 0;
            if (!ArtLibrary.CheckArtInstalled()) return false;
            if (!File.Exists(ScenePath))
            {
                Debug.LogError($"Bellwort Burrow: can't capture, {ScenePath} is missing.");
                return false;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;

            var setup = EditorSceneManager.GetSceneManagerSetup();
            bool canRestore = setup.Length > 0 && setup.All(s => !string.IsNullOrEmpty(s.path));
            Directory.CreateDirectory(Folder);
            var images = new List<string>();
            try
            {
                EditorUtility.DisplayProgressBar("Bellwort Burrow", "Checking the art rules", 0.1f);
                ArtRuleChecks.WriteReport(Path.Combine(Folder, "art-rules.txt"));

                EditorUtility.DisplayProgressBar("Bellwort Burrow", "Rendering the Forlorn Forest", 0.3f);
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                CaptureScene(images);

                EditorUtility.DisplayProgressBar("Bellwort Burrow", "Saving the sheets", 0.8f);
                CaptureSheets(images);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                if (canRestore) EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
            WriteRecord(images);
            count = images.Count;
            Debug.Log($"Bellwort Burrow: captured {count} canvas images into {Path.GetFullPath(Folder)}.");
            return true;
        }

        // ---------- Scene shots ----------

        static void CaptureScene(List<string> images)
        {
            var main = Camera.main;
            Color background = main != null ? main.backgroundColor : NightColor;
            int mask = main != null ? main.cullingMask : ~0;
            Rect ground = GroundBounds();
            float ppu = ArtLibrary.PixelsPerUnit;

            Vector2 start = main != null ? (Vector2)main.transform.position : ground.center;
            images.Add(Shot("opening-screen", Snap(start - new Vector2(ScreenWidth, ScreenHeight) / (2 * ppu)), ScreenWidth, ScreenHeight, background, mask));

            int width = Mathf.RoundToInt(ground.width * ppu), height = Mathf.RoundToInt(ground.height * ppu);
            images.Add(Shot("hamlet", Snap(ground.min), width, height, background, mask));

            var used = new HashSet<string>();
            foreach (var home in TreeHomes())
            {
                float w = ScreenWidth / ppu, h = 2 * ScreenHeight / ppu;
                float x = Mathf.Clamp(home.position.x - w / 2, ground.xMin, Mathf.Max(ground.xMin, ground.xMax - w));
                float y = Mathf.Clamp(home.position.y - FeetAboveBottom / ppu, ground.yMin, Mathf.Max(ground.yMin, ground.yMax - h));
                string name = $"walking-north-{home.name}";
                for (int i = 2; !used.Add(name); i++) name = $"walking-north-{home.name}-{i}";
                images.Add(Shot(name, Snap(new Vector2(x, y)), ScreenWidth, 2 * ScreenHeight, background, mask));
            }
        }

        /// <summary>The stacked tree home prefabs placed in the open scene (not the base pieces inside them).</summary>
        static List<(string name, Vector3 position)> TreeHomes()
        {
            var homes = new List<(string, Vector3)>();
            foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (!PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject)) continue;
                var source = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
                if (source == null) continue;
                string n = source.name;
                if (n.StartsWith("giant_") && n.Contains("_home_") && !n.EndsWith("_base")) homes.Add((n, t.position));
            }
            homes.Sort((a, b) => a.Item2.x.CompareTo(b.Item2.x));
            return homes;
        }

        /// <summary>The painted ground, in world units, from every tilemap in the open scene.</summary>
        static Rect GroundBounds()
        {
            bool any = false;
            Vector3 min = Vector3.zero, max = Vector3.zero;
            foreach (var map in UnityEngine.Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
            {
                var cells = map.cellBounds;
                if (cells.size.x <= 0 || cells.size.y <= 0) continue;
                Vector3 a = map.CellToWorld(cells.min), b = map.CellToWorld(cells.max);
                if (!any) { min = Vector3.Min(a, b); max = Vector3.Max(a, b); any = true; }
                else { min = Vector3.Min(min, Vector3.Min(a, b)); max = Vector3.Max(max, Vector3.Max(a, b)); }
            }
            if (!any) return new Rect(0, 0, ScreenWidth / ArtLibrary.PixelsPerUnit, ScreenHeight / ArtLibrary.PixelsPerUnit);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        static Vector2 Snap(Vector2 world) =>
            new Vector2(Mathf.Round(world.x * ArtLibrary.PixelsPerUnit), Mathf.Round(world.y * ArtLibrary.PixelsPerUnit)) / ArtLibrary.PixelsPerUnit;

        /// <summary>Renders width x height art pixels whose bottom left corner is at the given world point.</summary>
        static string Shot(string name, Vector2 bottomLeft, int width, int height, Color background, int mask)
        {
            float ppu = ArtLibrary.PixelsPerUnit;
            var go = EditorUtility.CreateGameObjectWithHideFlags("Bellwort Capture Camera", HideFlags.HideAndDontSave, typeof(Camera));
            var camera = go.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = height / 2f / ppu;
            camera.aspect = width / (float)height;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;
            camera.cullingMask = mask;
            camera.transform.position = new Vector3(bottomLeft.x + width / 2f / ppu, bottomLeft.y + height / 2f / ppu, -10f);

            var texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                filterMode = FilterMode.Point,
            };
            string file = name + ".png";
            try
            {
                var request = new RenderPipeline.StandardRequest();
                if (RenderPipeline.SupportsRenderRequest(camera, request))
                {
                    request.destination = texture;
                    RenderPipeline.SubmitRenderRequest(camera, request);
                }
                else
                {
                    camera.targetTexture = texture;
                    camera.Render();
                    camera.targetTexture = null;
                }
                SavePng(texture, Path.Combine(Folder, file));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                texture.Release();
                UnityEngine.Object.DestroyImmediate(texture);
            }
            return file;
        }

        static void SavePng(RenderTexture texture, string path)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = texture;
            var image = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(path, image.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(image);
        }

        // ---------- Sheets ----------

        static void CaptureSheets(List<string> images)
        {
            foreach (var path in SheetSources())
            {
                AsepriteSheet sheet;
                try { sheet = AsepriteSheet.Read(File.ReadAllBytes(path)); }
                catch (Exception e)
                {
                    Debug.LogWarning($"Bellwort Burrow: couldn't read {path} for the canvas: {e.Message}");
                    continue;
                }
                string file = $"sheet-{Path.GetFileNameWithoutExtension(path)}.png";
                File.WriteAllBytes(Path.Combine(Folder, file), ToPng(sheet.Art, sheet.Width, sheet.Height));
                images.Add(file);
            }
        }

        static IEnumerable<string> SheetSources()
        {
            foreach (var path in ArtLibrary.SheetPaths(false)) yield return path;
            foreach (var path in ArtLibrary.SheetPaths(true)) yield return path;
            if (Directory.Exists(ArtLibrary.CharacterFolder))
                foreach (var path in Directory.GetFiles(ArtLibrary.CharacterFolder, "*.aseprite").OrderBy(p => p, StringComparer.Ordinal))
                    yield return path.Replace('\\', '/');
        }

        /// <summary>Aseprite rows run top to bottom; Unity textures start at the bottom.</summary>
        static byte[] ToPng(byte[] rgba, int width, int height)
        {
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                int source = y * width * 4, target = (height - 1 - y) * width;
                for (int x = 0; x < width; x++, source += 4)
                    pixels[target + x] = new Color32(rgba[source], rgba[source + 1], rgba[source + 2], rgba[source + 3]);
            }
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            byte[] png = texture.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(texture);
            return png;
        }

        // ---------- Record ----------

        /// <summary>Every source file the Sync Ledger tracks: sheets, characters, palettes and zone scenes.</summary>
        public static List<string> LedgerSources()
        {
            var sources = new List<string>();
            void Add(string folder, string pattern)
            {
                if (!Directory.Exists(folder)) return;
                foreach (var path in Directory.GetFiles(folder, pattern))
                    sources.Add(path.Replace('\\', '/'));
            }
            Add(ArtLibrary.ArtRoot + "/Forest", "*.aseprite");
            Add(ArtLibrary.CharacterFolder, "*.aseprite");
            Add(ArtLibrary.ArtRoot + "/Palettes", "*.gpl");
            Add("Assets/Scenes/Zones", "*.unity");
            sources.Sort(StringComparer.Ordinal);
            return sources;
        }

        static void WriteRecord(List<string> images)
        {
            var json = new StringBuilder();
            json.Append("{\n");
            json.Append($"  \"captured\": \"{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}\",\n");
            json.Append("  \"sources\": {\n");
            var sources = LedgerSources();
            for (int i = 0; i < sources.Count; i++)
            {
                string fingerprint = ArtRules.Fingerprint(File.ReadAllBytes(sources[i]));
                json.Append($"    \"{sources[i]}\": \"{fingerprint}\"{(i < sources.Count - 1 ? "," : "")}\n");
            }
            json.Append("  },\n  \"images\": [\n");
            for (int i = 0; i < images.Count; i++)
                json.Append($"    \"{images[i]}\"{(i < images.Count - 1 ? "," : "")}\n");
            json.Append("  ]\n}\n");
            File.WriteAllText(Path.Combine(Folder, "captures.json"), json.ToString());
        }
    }
}
