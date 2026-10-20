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
using BellwortBurrow.Data;

namespace BellwortBurrow.EditorTools
{
    /// <summary>
    /// Bellwort Burrow > Canvas > Capture Shots: saves the images for the Bellwort Biome Palettes canvas into Captures/
    /// (next to Assets, ignored by git), straight from Unity and the sheets, so the canvas never shows redrawn art.
    /// - Scene shots of every zone scene in Assets/Scenes/Zones, rendered by a camera to a texture with Unity's own lighting
    ///   and sorting: the opening screen (what the Main Camera sees), the whole scene, and two screens stacked above each
    ///   tree home when the scene has any. Each image starts with the scene's name, for example forlorn-forest-opening-screen.
    /// - Every sheet and character exactly as drawn: the Art layer, pixel for pixel.
    /// - The critters put together with their faces and clothes, the way the game does it (players-faces, players-outfits,
    ///   wardrobe, and player-mouse and so on). The player files hold no face or clothes of their own.
    /// - captures.json: when, and the fingerprint of every source file the images came from, for the Sync Ledger.
    /// - art-rules.txt: what Check Art Rules found.
    /// Images are saved at 1 pixel per art pixel; the canvas scales them up with pixelated rendering.
    /// It opens each zone scene to render it and puts your open scenes back afterwards. It never saves a scene.
    /// A new zone scene is picked up on its own: save it in Assets/Scenes/Zones.
    /// </summary>
    public static class CanvasCapture
    {
        public const string Folder = "Captures";
        const string ZoneFolder = "Assets/Scenes/Zones";
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
            var scenes = ZoneScenes();
            if (scenes.Count == 0)
            {
                Debug.LogError($"Bellwort Burrow: can't capture, there are no scenes in {ZoneFolder}.");
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

                for (int i = 0; i < scenes.Count; i++)
                {
                    string name = Path.GetFileNameWithoutExtension(scenes[i]);
                    EditorUtility.DisplayProgressBar("Bellwort Burrow", $"Rendering {name}", 0.2f + 0.6f * i / scenes.Count);
                    EditorSceneManager.OpenScene(scenes[i], OpenSceneMode.Single);
                    CaptureScene(images, Slug(name));
                }

                EditorUtility.DisplayProgressBar("Bellwort Burrow", "Saving the sheets", 0.8f);
                CaptureSheets(images);
                CapturePlayerFaces(images);
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

        /// <summary>Every zone scene, in name order.</summary>
        static List<string> ZoneScenes() => Directory.Exists(ZoneFolder)
            ? Directory.GetFiles(ZoneFolder, "*.unity").Select(p => p.Replace('\\', '/')).OrderBy(p => p, StringComparer.Ordinal).ToList()
            : new List<string>();

        /// <summary>"ForlornForest" becomes "forlorn-forest", for the image names.</summary>
        static string Slug(string sceneName) =>
            System.Text.RegularExpressions.Regex.Replace(sceneName, "(?<=[a-z0-9])(?=[A-Z])", "-").ToLowerInvariant();

        static void CaptureScene(List<string> images, string prefix)
        {
            var main = Camera.main;
            Color background = main != null ? main.backgroundColor : NightColor;
            int mask = main != null ? main.cullingMask : ~0;
            Rect ground = GroundBounds();
            float ppu = ArtLibrary.PixelsPerUnit;

            Vector2 start = main != null ? (Vector2)main.transform.position : ground.center;
            images.Add(Shot($"{prefix}-opening-screen", Snap(start - new Vector2(ScreenWidth, ScreenHeight) / (2 * ppu)), ScreenWidth, ScreenHeight, background, mask));

            int width = Mathf.RoundToInt(ground.width * ppu), height = Mathf.RoundToInt(ground.height * ppu);
            images.Add(Shot($"{prefix}-whole", Snap(ground.min), width, height, background, mask));

            var used = new HashSet<string>();
            foreach (var home in TreeHomes())
            {
                float w = ScreenWidth / ppu, h = 2 * ScreenHeight / ppu;
                float x = Mathf.Clamp(home.position.x - w / 2, ground.xMin, Mathf.Max(ground.xMin, ground.xMax - w));
                float y = Mathf.Clamp(home.position.y - FeetAboveBottom / ppu, ground.yMin, Mathf.Max(ground.yMin, ground.yMax - h));
                string name = $"{prefix}-walking-north-{home.name}";
                for (int i = 2; !used.Add(name); i++) name = $"{prefix}-walking-north-{home.name}-{i}";
                images.Add(Shot(name, Snap(new Vector2(x, y)), ScreenWidth, 2 * ScreenHeight, background, mask));
            }
        }

        /// <summary>The giant trees in the open scene that have a ground tree house on them.</summary>
        static List<(string name, Vector3 position)> TreeHomes()
        {
            var homes = new List<(string, Vector3)>();
            foreach (var tree in UnityEngine.Object.FindObjectsByType<BellwortBurrow.Systems.World.GiantTree>())
            {
                bool hasHouse = false;
                foreach (var house in tree.GetComponentsInChildren<BellwortBurrow.Systems.World.TreeHouse>())
                    if (house.part == BellwortBurrow.Systems.World.TreeHouse.Part.House) hasHouse = true;
                if (!hasHouse) continue;
                var source = PrefabUtility.GetCorrespondingObjectFromSource(tree.gameObject);
                homes.Add((source != null ? source.name : tree.name, tree.transform.position));
            }
            homes.Sort((a, b) => a.Item2.x.CompareTo(b.Item2.x));
            return homes;
        }

        /// <summary>The painted ground, in world units, from every tilemap in the open scene.</summary>
        static Rect GroundBounds()
        {
            bool any = false;
            Vector3 min = Vector3.zero, max = Vector3.zero;
            foreach (var map in UnityEngine.Object.FindObjectsByType<Tilemap>())
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

        // ---------- Players, faces and clothes ----------

        const string CritterCatalogPath = "Assets/Data/Characters/CritterCatalog.asset";
        const int CellGap = 4;

        // The kind the wardrobe picture is dressed on (Jocelyn, Oct 20).
        const string WardrobeKindId = "fox";

        /// <summary>
        /// The critters put together the way Player Appearance does in the game, in natural colors: the player file, then
        /// the bottom (unless the top covers it), top, waist, neck, the face (no mouth for a kind with a beak) and the ears
        /// (moved to the kind's ears, mirrored for a pair, none on a kind without ears). Saves:
        /// - players-faces.png: a row per kind, a column per face, in the clothes a new character wears;
        /// - player-&lt;kind&gt;.png: each kind as a new character (the first face and the first piece of every slot);
        /// - players-outfits.png: a row per kind, and columns that step through every slot's pieces together, so each
        ///   piece shows on every kind;
        /// - wardrobe.png: a row per slot, every piece on the fox (Jocelyn, Oct 20; the first kind if there's no fox), with
        ///   the first piece of every other slot.
        /// </summary>
        static void CapturePlayerFaces(List<string> images)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CritterCatalog>(CritterCatalogPath);
            var faceList = catalog != null ? catalog.Faces.Where(face => face != null && face.eyes != null).ToList() : null;
            if (catalog == null || catalog.Palette == null || faceList.Count == 0) return;
            var wardrobe = catalog.Wardrobe;
            var kinds = catalog.Kinds.Where(kind => kind != null && kind.Sprite != null).ToList();
            if (kinds.Count == 0) return;

            var sheets = new Dictionary<string, AsepriteSheet>();
            var bodies = new List<AsepriteSheet>();
            try
            {
                foreach (var kind in kinds) bodies.Add(ReadSheet(sheets, kind.Sprite));
                ReadSheet(sheets, faceList[0].eyes);
                if (wardrobe != null)
                    foreach (var piece in wardrobe.Pieces.Where(p => p != null && p.sprite != null)) ReadSheet(sheets, piece.sprite);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Bellwort Burrow: couldn't read the player files, faces or clothes for the canvas: {e.Message}");
                return;
            }
            int width = bodies.Max(b => b.Width), height = bodies.Max(b => b.Height);
            var slots = (ClothingSlot[])Enum.GetValues(typeof(ClothingSlot));
            Dictionary<ClothingSlot, ClothingPiece> Outfit(Func<List<ClothingPiece>, ClothingPiece> pick) =>
                wardrobe == null ? new Dictionary<ClothingSlot, ClothingPiece>() : slots.ToDictionary(slot => slot, slot => pick(wardrobe.For(slot)));
            var firsts = Outfit(list => list.FirstOrDefault());

            byte[] Cell(int k, FaceStyle face, Dictionary<ClothingSlot, ClothingPiece> outfit) =>
                Dress(sheets, catalog, kinds[k], bodies[k], face, outfit);

            // Every face, in a new character's clothes; and each kind as a new character.
            var faceCells = new List<List<byte[]>>();
            for (int k = 0; k < kinds.Count; k++)
            {
                var row = faceList.Select(face => Cell(k, face, firsts)).ToList();
                faceCells.Add(row);
                string single = $"player-{Slug(kinds[k].Id)}.png";
                File.WriteAllBytes(Path.Combine(Folder, single), ToPng(row[0], width, height));
                images.Add(single);
            }
            SaveGrid(images, "players-faces.png", faceCells, width, height);
            if (wardrobe == null) return;

            // Every slot's pieces stepped through together, on every kind.
            int columns = slots.Max(slot => wardrobe.For(slot).Count);
            var outfitCells = new List<List<byte[]>>();
            for (int k = 0; k < kinds.Count; k++)
                outfitCells.Add(Enumerable.Range(0, columns).Select(i => Cell(k, faceList[0], Outfit(list => list.Count == 0 ? null : list[i % list.Count]))).ToList());
            SaveGrid(images, "players-outfits.png", outfitCells, width, height);

            // Every piece on the fox, one slot to a row.
            int model = Math.Max(0, kinds.FindIndex(kind => kind.Id == WardrobeKindId));
            var wardrobeCells = new List<List<byte[]>>();
            foreach (var slot in slots)
            {
                wardrobeCells.Add(wardrobe.For(slot).Select(piece =>
                {
                    var outfit = new Dictionary<ClothingSlot, ClothingPiece>(firsts) { [slot] = piece };
                    return Cell(model, faceList[0], outfit);
                }).ToList());
            }
            SaveGrid(images, "wardrobe.png", wardrobeCells, width, height);
        }

        static AsepriteSheet ReadSheet(Dictionary<string, AsepriteSheet> sheets, Sprite sprite)
        {
            string path = AssetDatabase.GetAssetPath(sprite);
            if (!sheets.TryGetValue(path, out var sheet)) sheets[path] = sheet = AsepriteSheet.Read(File.ReadAllBytes(path));
            return sheet;
        }

        /// <summary>One critter dressed and wearing a face, in natural colors, as Player Appearance puts it together.</summary>
        static byte[] Dress(Dictionary<string, AsepriteSheet> sheets, CritterCatalog catalog, CritterKind kind, AsepriteSheet body,
            FaceStyle face, Dictionary<ClothingSlot, ClothingPiece> outfit)
        {
            var cell = (byte[])body.Art.Clone();
            var none = new List<Color32>();
            void Put(Sprite part, List<Color32> from, List<Color32> to, int dx = 0, int dy = 0, bool flip = false)
            {
                if (part != null) Overlay(cell, body.Width, body.Height, sheets[AssetDatabase.GetAssetPath(part)], part, from, to, dx, dy, flip);
            }
            outfit.TryGetValue(ClothingSlot.Top, out var top);
            outfit.TryGetValue(ClothingSlot.Bottom, out var bottom);
            if (top == null || !top.coversBottom) Put(bottom?.sprite, none, none);
            Put(top?.sprite, none, none);
            if (outfit.TryGetValue(ClothingSlot.Waist, out var waist)) Put(waist?.sprite, none, none);
            if (outfit.TryGetValue(ClothingSlot.Neck, out var neck)) Put(neck?.sprite, none, none);

            var from = new List<Color32>();
            var to = new List<Color32>();
            RampSwap.BuildFace(kind, new PlayerProfile { critterKind = kind.Id }, catalog.Palette, catalog.FaceFurRamp, catalog.FaceEyeRamp, from, to);
            Put(face.eyes, from, to);
            if (kind.ShowsMouth) Put(face.mouth, from, to);

            if (kind.HasEars && catalog.Wardrobe != null && outfit.TryGetValue(ClothingSlot.Ears, out var ears) && ears?.sprite != null)
            {
                var shift = kind.EarAnchor - catalog.Wardrobe.EarAnchor;
                Put(ears.sprite, none, none, shift.x, shift.y);
                if (ears.paired) Put(ears.sprite, none, none, -shift.x, shift.y, flip: true);
            }
            return cell;
        }

        static void SaveGrid(List<string> images, string file, List<List<byte[]>> cells, int cellWidth, int cellHeight)
        {
            int columns = cells.Max(row => row.Count);
            int width = columns * (cellWidth + CellGap) - CellGap, height = cells.Count * (cellHeight + CellGap) - CellGap;
            var grid = new byte[width * height * 4];
            for (int row = 0; row < cells.Count; row++)
                for (int column = 0; column < cells[row].Count; column++)
                    Blit(grid, width, cells[row][column], cellWidth, cellHeight, column * (cellWidth + CellGap), row * (cellHeight + CellGap));
            File.WriteAllBytes(Path.Combine(Folder, file), ToPng(grid, width, height));
            images.Add(file);
        }

        /// <summary>
        /// Draws one part's slice over a player frame, lined up by their bottom middle pivots, swapping colors. dx and dy
        /// move it (y down); flip mirrors it across the frame's middle first, as a flipped SpriteRenderer does.
        /// </summary>
        static void Overlay(byte[] cell, int width, int height, AsepriteSheet faces, Sprite part, List<Color32> from, List<Color32> to,
            int dx = 0, int dy = 0, bool flip = false)
        {
            if (part == null) return;
            var slice = faces.Slices.FirstOrDefault(s => s.Name == part.name);
            if (slice.Name == null)
            {
                Debug.LogWarning($"Bellwort Burrow: no slice named {part.name} for the canvas.");
                return;
            }
            int left = width / 2 - (slice.HasPivot ? slice.PivotX : slice.Width / 2);
            int top = height - (slice.HasPivot ? slice.PivotY : slice.Height);
            for (int y = 0; y < slice.Height; y++)
            for (int x = 0; x < slice.Width; x++)
            {
                int source = ((slice.Y + y) * faces.Width + slice.X + x) * 4;
                int tx = left + (flip ? slice.Width - 1 - x : x) + dx, ty = top + y + dy;
                if (faces.Art[source + 3] == 0 || tx < 0 || ty < 0 || tx >= width || ty >= height) continue;
                var color = new Color32(faces.Art[source], faces.Art[source + 1], faces.Art[source + 2], 255);
                int swap = from.IndexOf(color);
                if (swap >= 0) color = to[swap];
                int target = (ty * width + tx) * 4;
                cell[target] = color.r; cell[target + 1] = color.g; cell[target + 2] = color.b; cell[target + 3] = 255;
            }
        }

        static void Blit(byte[] grid, int gridWidth, byte[] cell, int width, int height, int left, int top)
        {
            for (int y = 0; y < height; y++)
                Buffer.BlockCopy(cell, y * width * 4, grid, ((top + y) * gridWidth + left) * 4, width * 4);
        }

        static IEnumerable<string> SheetSources()
        {
            foreach (var path in ArtLibrary.SheetPaths(false)) yield return path;
            foreach (var path in ArtLibrary.SheetPaths(true)) yield return path;
            if (Directory.Exists(ArtLibrary.CharacterFolder))
                foreach (var path in Directory.GetFiles(ArtLibrary.CharacterFolder, "*.aseprite", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal))
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

        /// <summary>Every source file the Sync Ledger tracks: sheets, backdrops, characters (subfolders too, like Player), palettes and zone scenes.</summary>
        public static List<string> LedgerSources()
        {
            var sources = new List<string>();
            void Add(string folder, string pattern)
            {
                if (!Directory.Exists(folder)) return;
                foreach (var path in Directory.GetFiles(folder, pattern, SearchOption.AllDirectories))
                    sources.Add(path.Replace('\\', '/'));
            }
            Add(ArtLibrary.ArtRoot + "/Forest", "*.aseprite");
            Add(ArtLibrary.ArtRoot + "/Burrow", "*.aseprite");
            Add(ArtLibrary.ArtRoot + "/Backdrops", "*.aseprite");
            Add(ArtLibrary.CharacterFolder, "*.aseprite");
            Add(ArtLibrary.ArtRoot + "/Palettes", "*.gpl");
            Add(ZoneFolder, "*.unity");
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
