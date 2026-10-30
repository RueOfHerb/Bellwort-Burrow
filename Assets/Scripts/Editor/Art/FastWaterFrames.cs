using System.Collections.Generic;
using System.IO;
using System.Linq;
using BellwortBurrow.Tiles;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace BellwortBurrow.EditorTools
{
    /// <summary>
    /// Makes Fast Water (art-4) from a ground sheet: for every layout of the calm water's set (water_set), its piece
    /// with the glints taken off (<see cref="Calm"/>), then that piece with each frame of each look of the
    /// fast_water_streaks slice laid over its open water (<see cref="WithStreaks"/>). The frames go on a texture of
    /// their own, "Burrow Fast Water Frames", which setup remakes each time with the tile, so the banks always match
    /// the calm water's pieces. The streaks slice has the frames across and the looks down; a calm look comes after
    /// them (<see cref="FastWaterTile.lookWeights"/>).
    /// </summary>
    public static class FastWaterFrames
    {
        public const string StreaksSlice = "fast_water_streaks";
        const int Size = 16, Columns = 64;

        /// <summary>How often each streaked look comes up against the calm one (Jocelyn's pick, Oct 29: 2 each, calm 3).</summary>
        public const int LookWeight = 2, CalmWeight = 3;

        /// <summary>Frames a second: 140 ms a frame.</summary>
        public const float FramesPerSecond = 1000f / 140f;

        /// <summary>A 16 x 16 block of a sheet's art, rows top to bottom.</summary>
        public static Color32[] Block(AsepriteSheet sheet, int x, int y)
        {
            var block = new Color32[Size * Size];
            for (int py = 0; py < Size; py++)
                for (int px = 0; px < Size; px++)
                {
                    int i = ((y + py) * sheet.Width + x + px) * 4;
                    block[py * Size + px] = new Color32(sheet.Art[i], sheet.Art[i + 1], sheet.Art[i + 2], sheet.Art[i + 3]);
                }
            return block;
        }

        static bool Same(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;

        /// <summary>The open water's colour: the commonest opaque colour of the given pieces (the full water pieces).</summary>
        public static Color32 OpenWater(IEnumerable<Color32[]> pieces) =>
            pieces.SelectMany(p => p).Where(c => c.a == 255)
                  .GroupBy(c => (c.r, c.g, c.b)).OrderByDescending(g => g.Count()).Select(g => g.First()).FirstOrDefault();

        /// <summary>
        /// A water piece with its glints taken off: a pixel with open water straight above and below it in the piece
        /// (a dash one pixel tall on the open water) becomes open water. Bands two or more pixels tall stay.
        /// </summary>
        public static Color32[] Calm(Color32[] piece, Color32 open)
        {
            var calm = (Color32[])piece.Clone();
            for (int y = 1; y < Size - 1; y++)
                for (int x = 0; x < Size; x++)
                {
                    var c = piece[y * Size + x];
                    if (c.a == 0 || Same(c, open)) continue;
                    if (Same(piece[(y - 1) * Size + x], open) && Same(piece[(y + 1) * Size + x], open)) calm[y * Size + x] = open;
                }
            return calm;
        }

        /// <summary>A calm piece with a streak frame over it: the streak's pixels go only where the piece is open water.</summary>
        public static Color32[] WithStreaks(Color32[] calm, Color32[] streak, Color32 open)
        {
            var frame = (Color32[])calm.Clone();
            for (int i = 0; i < frame.Length; i++)
                if (streak[i].a == 255 && Same(calm[i], open)) frame[i] = streak[i];
            return frame;
        }

        /// <summary>
        /// Makes the frames for these rules' pieces (each a cell of the sheet, top-left corner) and saves them on the
        /// zone's frames texture. Returns every rule's frames in order (each look's, then the calm one) and how many
        /// looks the streaks slice has, or null when the sheet has no streaks slice.
        /// </summary>
        public static (Sprite[] frames, int looks, int framesPerLook)? Make(AsepriteSheet sheet, IList<(int x, int y, Sprite piece)> rules,
                                                                            IEnumerable<(int x, int y)> fullPieces, string texturePath)
        {
            int at = sheet.Slices.FindIndex(s => s.Name == StreaksSlice);
            if (at < 0) return null;
            var streaks = sheet.Slices[at];
            int framesPerLook = streaks.Width / Size, looks = streaks.Height / Size;
            if (framesPerLook == 0 || looks == 0) return null;
            var open = OpenWater(fullPieces.Select(p => Block(sheet, p.x, p.y)));
            int stride = looks * framesPerLook + 1, count = rules.Count * stride;
            int rows = (count + Columns - 1) / Columns;
            var texture = new Texture2D(Columns * Size, Mathf.Max(1, rows) * Size, TextureFormat.RGBA32, false)
            {
                name = Path.GetFileNameWithoutExtension(texturePath),
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color32[texture.width * texture.height];
            var placed = new List<(int cell, string name, Vector2 pivot)>();
            int cellIndex = 0;
            void Put(Color32[] block, string name, Vector2 pivot)
            {
                int cx = cellIndex % Columns, cy = cellIndex / Columns;
                for (int py = 0; py < Size; py++)
                    for (int px = 0; px < Size; px++)
                        pixels[(texture.height - 1 - (cy * Size + py)) * texture.width + cx * Size + px] = block[py * Size + px];
                placed.Add((cellIndex, name, pivot));
                cellIndex++;
            }
            for (int r = 0; r < rules.Count; r++)
            {
                var (x, y, piece) = rules[r];
                var pivot = piece != null ? new Vector2(piece.pivot.x / piece.rect.width, piece.pivot.y / piece.rect.height) : new Vector2(0.5f, 0.5f);
                var calm = Calm(Block(sheet, x, y), open);
                for (int look = 0; look < looks; look++)
                    for (int f = 0; f < framesPerLook; f++)
                        Put(WithStreaks(calm, Block(sheet, streaks.X + f * Size, streaks.Y + look * Size), open), $"fast_water_{r:00}_{look}_{f}", pivot);
                Put(calm, $"fast_water_{r:00}_calm", pivot);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            if (AssetDatabase.LoadMainAssetAtPath(texturePath) != null) AssetDatabase.DeleteAsset(texturePath);
            AssetDatabase.CreateAsset(texture, texturePath);
            var sprites = new Sprite[placed.Count];
            foreach (var (cell, name, pivot) in placed)
            {
                int cx = cell % Columns, cy = cell / Columns;
                var rect = new Rect(cx * Size, texture.height - (cy + 1) * Size, Size, Size);
                var sprite = Sprite.Create(texture, rect, pivot, Size, 0, SpriteMeshType.FullRect);
                sprite.name = name;
                AssetDatabase.AddObjectToAsset(sprite, texture);
                sprites[cell] = sprite;
            }
            AssetDatabase.SaveAssets();
            return (sprites, looks, framesPerLook);
        }
    }
}
