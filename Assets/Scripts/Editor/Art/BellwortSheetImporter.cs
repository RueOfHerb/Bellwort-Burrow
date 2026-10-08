using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;

namespace BellwortBurrow.EditorTools
{
    /// <summary>
    /// Imports a Bellwort art sheet (.aseprite) and turns every Aseprite slice into a named sprite.
    /// Unity's own Aseprite importer ignores slices, so sheets use this importer instead
    /// (their .meta files select it; characters keep Unity's importer for animation).
    ///
    /// - Art: every visible layer except glow layers, flattened.
    /// - Glow: layers with "glow" in their name. Each slice with glow pixels also gets a "name_glow" sprite
    ///   with the same rect and pivot, meant for an unlit material so it shines through the night.
    /// - Tile Size above 0 cuts each slice into tiles named "name_00", "name_01"... (row by row)
    ///   and makes a Tile asset for each, ready for the Tile Palette.
    /// </summary>
    [ScriptedImporter(1, new[] { "bwsheet" }, new[] { "aseprite", "ase" })]
    public class BellwortSheetImporter : ScriptedImporter
    {
        [Tooltip("Pixels per world unit. 16 means one 16 px tile is one unit.")]
        public float pixelsPerUnit = 16f;

        [Tooltip("0: each slice becomes one sprite. 16: each slice is cut into 16 px tiles, and each tile also becomes a Tile asset.")]
        public int tileSize = 0;

        public override void OnImportAsset(AssetImportContext ctx)
        {
            AsepriteSheet sheet;
            try
            {
                sheet = AsepriteSheet.Read(File.ReadAllBytes(ctx.assetPath));
            }
            catch (System.Exception e)
            {
                ctx.LogImportError($"Could not read {ctx.assetPath}: {e.Message}");
                return;
            }
            foreach (var warning in sheet.Warnings)
                ctx.LogImportWarning(warning);

            string sheetName = Path.GetFileNameWithoutExtension(ctx.assetPath);
            var artTexture = MakeTexture(sheet.Art, sheet.Width, sheet.Height, sheetName);
            ctx.AddObjectToAsset("texture", artTexture, artTexture);
            ctx.SetMainObject(artTexture);

            Texture2D glowTexture = null;
            if (sheet.HasGlow)
            {
                glowTexture = MakeTexture(sheet.Glow, sheet.Width, sheet.Height, sheetName + "_glow");
                ctx.AddObjectToAsset("glow texture", glowTexture);
            }

            var slices = sheet.Slices;
            if (slices.Count == 0)
            {
                ctx.LogImportWarning($"{sheetName} has no slices, so the whole canvas becomes one sprite. Add slices in Aseprite with the Slice tool (C).");
                slices = new List<SheetSlice> { new SheetSlice { Name = sheetName, Width = sheet.Width, Height = sheet.Height } };
            }

            var usedNames = new HashSet<string>();
            foreach (var original in slices)
            {
                var slice = Clamp(original, sheet, ctx);
                if (slice.Width <= 0 || slice.Height <= 0) continue;
                if (tileSize > 0) ImportTiles(ctx, sheet, slice, artTexture, usedNames);
                else ImportSprite(ctx, sheet, slice, artTexture, glowTexture, usedNames);
            }
        }

        void ImportSprite(AssetImportContext ctx, AsepriteSheet sheet, SheetSlice slice, Texture2D art, Texture2D glow, HashSet<string> usedNames)
        {
            string name = Unique(slice.Name, usedNames);
            var rect = ToUnityRect(slice, sheet.Height);
            // Aseprite pivots count down from the slice's top; Unity's count up from the bottom.
            var pivot = slice.HasPivot
                ? new Vector2(slice.PivotX / (float)slice.Width, (slice.Height - slice.PivotY) / (float)slice.Height)
                : new Vector2(0.5f, 0f);

            ctx.AddObjectToAsset("sprite/" + name, MakeSprite(art, rect, pivot, name));

            if (glow != null && AsepriteSheet.AnyAlpha(sheet.Glow, sheet.Width, slice.X, slice.Y, slice.Width, slice.Height))
                ctx.AddObjectToAsset("glow/" + name, MakeSprite(glow, rect, pivot, name + "_glow"));
        }

        void ImportTiles(AssetImportContext ctx, AsepriteSheet sheet, SheetSlice slice, Texture2D art, HashSet<string> usedNames)
        {
            int columns = slice.Width / tileSize, rows = slice.Height / tileSize;
            if (slice.Width % tileSize != 0 || slice.Height % tileSize != 0)
                ctx.LogImportWarning($"Slice \"{slice.Name}\" is {slice.Width}x{slice.Height}, not a multiple of {tileSize}; the extra pixels are skipped.");
            int count = columns * rows;
            string digits = new string('0', Mathf.Max(2, count.ToString().Length));

            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    int x = slice.X + column * tileSize, y = slice.Y + row * tileSize;
                    if (!AsepriteSheet.AnyAlpha(sheet.Art, sheet.Width, x, y, tileSize, tileSize)) continue;

                    int index = row * columns + column;
                    string name = Unique(count == 1 ? slice.Name : slice.Name + "_" + index.ToString(digits), usedNames);
                    var rect = new Rect(x, sheet.Height - y - tileSize, tileSize, tileSize);
                    var sprite = MakeSprite(art, rect, new Vector2(0.5f, 0.5f), name);
                    ctx.AddObjectToAsset("sprite/" + name, sprite);

                    var tile = ScriptableObject.CreateInstance<Tile>();
                    tile.name = name;
                    tile.sprite = sprite;
                    tile.colliderType = Tile.ColliderType.None;
                    ctx.AddObjectToAsset("tile/" + name, tile);
                }
            }
        }

        Sprite MakeSprite(Texture2D texture, Rect rect, Vector2 pivot, string name)
        {
            var sprite = Sprite.Create(texture, rect, pivot, pixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            return sprite;
        }

        static Texture2D MakeTexture(byte[] rgba, int width, int height, string name)
        {
            // Aseprite rows run top to bottom; Unity textures start at the bottom.
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                int source = y * width * 4;
                int target = (height - 1 - y) * width;
                for (int x = 0; x < width; x++, source += 4)
                    pixels[target + x] = new Color32(rgba[source], rgba[source + 1], rgba[source + 2], rgba[source + 3]);
            }
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        static Rect ToUnityRect(SheetSlice slice, int sheetHeight) =>
            new Rect(slice.X, sheetHeight - slice.Y - slice.Height, slice.Width, slice.Height);

        static SheetSlice Clamp(SheetSlice slice, AsepriteSheet sheet, AssetImportContext ctx)
        {
            int x0 = Mathf.Max(0, slice.X), y0 = Mathf.Max(0, slice.Y);
            int x1 = Mathf.Min(sheet.Width, slice.X + slice.Width), y1 = Mathf.Min(sheet.Height, slice.Y + slice.Height);
            if (x0 == slice.X && y0 == slice.Y && x1 == slice.X + slice.Width && y1 == slice.Y + slice.Height) return slice;

            ctx.LogImportWarning($"Slice \"{slice.Name}\" reaches past the canvas and was trimmed to fit.");
            var clamped = slice;
            clamped.X = x0; clamped.Y = y0;
            clamped.Width = x1 - x0; clamped.Height = y1 - y0;
            clamped.PivotX = slice.PivotX - (x0 - slice.X);
            clamped.PivotY = slice.PivotY - (y0 - slice.Y);
            return clamped;
        }

        static string Unique(string name, HashSet<string> used)
        {
            if (string.IsNullOrWhiteSpace(name)) name = "slice";
            string candidate = name;
            for (int i = 2; !used.Add(candidate); i++) candidate = name + "_" + i;
            return candidate;
        }
    }
}
