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
    /// It makes the calm water's idle frames too (<see cref="MakeIdle"/>, Jocelyn, Oct 30): every water piece, glints
    /// and all, with each frame of each look of the idle_water_squiggles slice on its open water, onto "Burrow Water
    /// Frames". A frame where no squiggle lands on open water is the piece itself, and frames that come out the same
    /// share one sprite, so the texture only holds the frames that differ.
    /// </summary>
    public static class FastWaterFrames
    {
        public const string StreaksSlice = "fast_water_streaks";
        public const string SquigglesSlice = "idle_water_squiggles";

        /// <summary>The idle looks' weights against the still look, and the idle frames a second (200 ms a frame).</summary>
        public const int IdleLookWeight = 1, IdleStillWeight = 4;
        public const float IdleFramesPerSecond = 5f;
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
            var blocks = new List<(Color32[] pixels, string name, Vector2 pivot)>();
            for (int r = 0; r < rules.Count; r++)
            {
                var (x, y, piece) = rules[r];
                var pivot = PivotOf(piece);
                var calm = Calm(Block(sheet, x, y), open);
                for (int look = 0; look < looks; look++)
                    for (int f = 0; f < framesPerLook; f++)
                        blocks.Add((WithStreaks(calm, Block(sheet, streaks.X + f * Size, streaks.Y + look * Size), open), $"fast_water_{r:00}_{look}_{f}", pivot));
                blocks.Add((calm, $"fast_water_{r:00}_calm", pivot));
            }
            var sprites = Save(blocks, texturePath);
            return (sprites, looks, framesPerLook);
        }

        static Vector2 PivotOf(Sprite piece) =>
            piece != null ? new Vector2(piece.pivot.x / piece.rect.width, piece.pivot.y / piece.rect.height) : new Vector2(0.5f, 0.5f);

        static bool SameBlock(Color32[] a, Color32[] b)
        {
            for (int i = 0; i < a.Length; i++) if (!Same(a[i], b[i])) return false;
            return true;
        }

        /// <summary>
        /// Saves blocks on a texture of their own at this path (replacing it), as sprites; a block that comes out the
        /// same as an earlier one shares its sprite. Returns a sprite per block, in order.
        /// </summary>
        static Sprite[] Save(IList<(Color32[] pixels, string name, Vector2 pivot)> blocks, string texturePath)
        {
            var unique = new List<int>();                    // index into blocks of each block that gets a cell
            var cellOf = new int[blocks.Count];
            var byHash = new Dictionary<int, List<int>>();
            for (int i = 0; i < blocks.Count; i++)
            {
                int h = 17;
                foreach (var c in blocks[i].pixels) h = h * 31 + (c.r | c.g << 8 | c.b << 16 | c.a << 24);
                h = h * 31 + blocks[i].pivot.GetHashCode();
                if (!byHash.TryGetValue(h, out var same)) byHash[h] = same = new List<int>();
                int found = same.FindIndex(j => SameBlock(blocks[unique[j]].pixels, blocks[i].pixels) && blocks[unique[j]].pivot == blocks[i].pivot);
                if (found >= 0) { cellOf[i] = same[found]; continue; }
                same.Add(unique.Count);
                cellOf[i] = unique.Count;
                unique.Add(i);
            }
            int rows = Mathf.Max(1, (unique.Count + Columns - 1) / Columns);
            // Scenes keep each animated cell's frames by reference, so the texture and its sprites are updated in
            // place: the same asset, and the same sprite wherever a frame keeps its name, place and pivot.
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            bool isNew = texture == null;
            if (isNew) texture = new Texture2D(Columns * Size, rows * Size, TextureFormat.RGBA32, false);
            else texture.Reinitialize(Columns * Size, rows * Size, TextureFormat.RGBA32, false);
            texture.name = Path.GetFileNameWithoutExtension(texturePath);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[texture.width * texture.height];
            for (int cell = 0; cell < unique.Count; cell++)
            {
                int cx = cell % Columns, cy = cell / Columns;
                var block = blocks[unique[cell]].pixels;
                for (int py = 0; py < Size; py++)
                    for (int px = 0; px < Size; px++)
                        pixels[(texture.height - 1 - (cy * Size + py)) * texture.width + cx * Size + px] = block[py * Size + px];
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            if (isNew) AssetDatabase.CreateAsset(texture, texturePath);

            var old = new Dictionary<string, Sprite>();
            foreach (var sprite in AssetDatabase.LoadAllAssetRepresentationsAtPath(texturePath).OfType<Sprite>())
                if (!old.ContainsKey(sprite.name)) old[sprite.name] = sprite;
            var kept = new HashSet<Sprite>();
            var cellSprites = new Sprite[unique.Count];
            for (int cell = 0; cell < unique.Count; cell++)
            {
                int cx = cell % Columns, cy = cell / Columns;
                var (_, name, pivot) = blocks[unique[cell]];
                var rect = new Rect(cx * Size, texture.height - (cy + 1) * Size, Size, Size);
                if (old.TryGetValue(name, out var same) && same.rect == rect && same.texture == texture
                    && Vector2.Distance(same.pivot, pivot * Size) < 0.01f && Mathf.Approximately(same.pixelsPerUnit, Size))
                {
                    cellSprites[cell] = same;
                    kept.Add(same);
                    continue;
                }
                var made = Sprite.Create(texture, rect, pivot, Size, 0, SpriteMeshType.FullRect);
                made.name = name;
                AssetDatabase.AddObjectToAsset(made, texture);
                cellSprites[cell] = made;
            }
            foreach (var sprite in old.Values)
                if (!kept.Contains(sprite)) { AssetDatabase.RemoveObjectFromAsset(sprite); Object.DestroyImmediate(sprite, true); }
            EditorUtility.SetDirty(texture);
            AssetDatabase.SaveAssets();
            var result = new Sprite[blocks.Count];
            for (int i = 0; i < blocks.Count; i++) result[i] = cellSprites[cellOf[i]];
            return result;
        }

        /// <summary>
        /// Makes the calm water's idle frames for these pieces (each a cell of the sheet, top-left corner, with its
        /// sprite) and saves the ones that differ from their piece on the zone's frames texture. Returns every piece's
        /// frames in order (each look's 16 frames in turn; a frame with no squiggle on the open water is the piece's
        /// own sprite) and the look and frame counts, or null when the sheet has no squiggles slice.
        /// </summary>
        public static (Sprite[] frames, int looks, int framesPerLook)? MakeIdle(AsepriteSheet sheet, IList<(int x, int y, Sprite piece)> pieces,
                                                                                IEnumerable<(int x, int y)> fullPieces, string texturePath)
        {
            int at = sheet.Slices.FindIndex(s => s.Name == SquigglesSlice);
            if (at < 0) return null;
            var squiggles = sheet.Slices[at];
            int framesPerLook = squiggles.Width / Size, looks = squiggles.Height / Size;
            if (framesPerLook == 0 || looks == 0) return null;
            var open = OpenWater(fullPieces.Select(p => Block(sheet, p.x, p.y)));
            var overlays = new Color32[looks, framesPerLook][];
            for (int look = 0; look < looks; look++)
                for (int f = 0; f < framesPerLook; f++)
                    overlays[look, f] = Block(sheet, squiggles.X + f * Size, squiggles.Y + look * Size);

            var frames = new Sprite[pieces.Count * looks * framesPerLook];
            var blocks = new List<(Color32[] pixels, string name, Vector2 pivot)>();
            var slots = new List<int>();                     // which frame each block fills
            for (int p = 0; p < pieces.Count; p++)
            {
                var (x, y, piece) = pieces[p];
                var still = Block(sheet, x, y);
                for (int look = 0; look < looks; look++)
                    for (int f = 0; f < framesPerLook; f++)
                    {
                        int slot = (p * looks + look) * framesPerLook + f;
                        var frame = WithStreaks(still, overlays[look, f], open);
                        if (SameBlock(frame, still)) { frames[slot] = piece; continue; }
                        blocks.Add((frame, $"idle_water_{p:000}_{look}_{f:00}", PivotOf(piece)));
                        slots.Add(slot);
                    }
            }
            var made = Save(blocks, texturePath);
            for (int i = 0; i < made.Length; i++) frames[slots[i]] = made[i];
            return (frames, looks, framesPerLook);
        }
    }
}
