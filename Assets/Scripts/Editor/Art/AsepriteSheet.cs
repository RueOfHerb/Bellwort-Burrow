using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace BellwortBurrow.EditorTools
{
    /// <summary>A named rectangle on a sheet, read from an Aseprite slice. Coordinates are in pixels from the top left.</summary>
    public struct SheetSlice
    {
        public string Name;
        public int X, Y, Width, Height;
        public bool HasPivot;
        public int PivotX, PivotY; // relative to the slice origin, y down
    }

    /// <summary>
    /// Reads the first frame of an Aseprite file into two flattened RGBA images plus its slices.
    /// Layers whose name (or parent group name) contains "glow" go to <see cref="Glow"/>, every other visible layer
    /// goes to <see cref="Art"/>. Plain C# with no Unity types so it can be tested outside the editor.
    /// File format: https://github.com/aseprite/aseprite/blob/main/docs/ase-file-specs.md
    /// </summary>
    public sealed class AsepriteSheet
    {
        public int Width, Height;
        /// <summary>RGBA bytes, rows from top to bottom.</summary>
        public byte[] Art, Glow;
        public bool HasGlow;
        public readonly List<SheetSlice> Slices = new List<SheetSlice>();
        public readonly List<string> Warnings = new List<string>();

        const ushort FileMagic = 0xA5E0, FrameMagic = 0xF1FA;
        const ushort ChunkOldPalette = 0x0004, ChunkLayer = 0x2004, ChunkCel = 0x2005, ChunkPalette = 0x2019, ChunkSlice = 0x2022;
        const int LayerVisible = 1, LayerBackground = 8, LayerReference = 64;

        sealed class LayerInfo
        {
            public int Flags, Type, Level, Blend, Opacity;
            public string Name;
            public bool Visible, IsGlow;
        }

        sealed class CelInfo
        {
            public int Layer, X, Y, Opacity, ZIndex, Width, Height, Order;
            public byte[] Rgba;
        }

        public static AsepriteSheet Read(byte[] data)
        {
            var sheet = new AsepriteSheet();
            using (var reader = new BinaryReader(new MemoryStream(data)))
                sheet.Parse(reader);
            return sheet;
        }

        void Parse(BinaryReader r)
        {
            r.ReadUInt32(); // file size
            if (r.ReadUInt16() != FileMagic) throw new InvalidDataException("Not an Aseprite file.");
            int frames = r.ReadUInt16();
            Width = r.ReadUInt16();
            Height = r.ReadUInt16();
            int depth = r.ReadUInt16();
            uint flags = r.ReadUInt32();
            r.BaseStream.Position = 28;
            int transparentIndex = r.ReadByte();
            r.BaseStream.Position = 128;
            bool layerOpacityValid = (flags & 1) != 0;
            if (depth != 32 && depth != 16 && depth != 8) throw new InvalidDataException($"Unsupported color depth {depth}.");

            var palette = new byte[256 * 4];
            var oldPalette = new byte[256 * 4];
            bool hasNewPalette = false;
            var layers = new List<LayerInfo>();
            var cels = new List<CelInfo>();
            var rawCels = new List<(long pos, long end)>();
            var stream = r.BaseStream;

            for (int f = 0; f < frames; f++)
            {
                long frameStart = stream.Position;
                uint frameBytes = r.ReadUInt32();
                if (r.ReadUInt16() != FrameMagic) throw new InvalidDataException("Broken frame header.");
                int oldChunks = r.ReadUInt16();
                r.ReadUInt16(); // duration
                r.ReadBytes(2);
                uint newChunks = r.ReadUInt32();
                long chunkCount = newChunks != 0 ? newChunks : oldChunks;

                for (long c = 0; c < chunkCount; c++)
                {
                    long chunkStart = stream.Position;
                    uint chunkSize = r.ReadUInt32();
                    int type = r.ReadUInt16();
                    long chunkEnd = chunkStart + chunkSize;
                    if (chunkSize < 6 || chunkEnd > stream.Length) break;

                    if (type == ChunkPalette) { ReadPalette(r, palette); hasNewPalette = true; }
                    else if (type == ChunkOldPalette) ReadOldPalette(r, oldPalette);
                    else if (type == ChunkLayer && f == 0) layers.Add(ReadLayer(r, layerOpacityValid));
                    else if (type == ChunkCel && f == 0) rawCels.Add((stream.Position, chunkEnd));
                    else if (type == ChunkSlice) ReadSlice(r);

                    stream.Position = chunkEnd;
                }
                stream.Position = frameStart + frameBytes;
            }

            if (!hasNewPalette) palette = oldPalette;
            ResolveLayerTree(layers);

            // Cels are decoded after the whole file is read so the palette is final for indexed sprites.
            foreach (var (pos, end) in rawCels)
            {
                stream.Position = pos;
                var cel = ReadCel(r, end, depth, palette, transparentIndex, layers);
                if (cel != null) cels.Add(cel);
            }

            // Draw order: layer index + z-index, ties broken by z-index (Aseprite 1.3 rule).
            for (int i = 0; i < cels.Count; i++) cels[i].Order = i;
            cels.Sort((a, b) =>
            {
                int ka = a.Layer + a.ZIndex, kb = b.Layer + b.ZIndex;
                if (ka != kb) return ka.CompareTo(kb);
                if (a.ZIndex != b.ZIndex) return a.ZIndex.CompareTo(b.ZIndex);
                return a.Order.CompareTo(b.Order);
            });

            var art = new double[Width * Height * 4];
            var glow = new double[Width * Height * 4];
            var warnedBlend = false;
            foreach (var cel in cels)
            {
                var layer = layers[cel.Layer];
                if (!layer.Visible || layer.Type != 0) continue;
                if (layer.Blend != 0 && !warnedBlend)
                {
                    Warnings.Add($"Layer \"{layer.Name}\" uses a blend mode; it is imported as Normal.");
                    warnedBlend = true;
                }
                Composite(layer.IsGlow ? glow : art, cel, layer.Opacity);
            }

            Art = ToBytes(art);
            Glow = ToBytes(glow);
            for (int i = 3; i < Glow.Length; i += 4)
                if (Glow[i] != 0) { HasGlow = true; break; }
        }

        static void ResolveLayerTree(List<LayerInfo> layers)
        {
            var visibleAt = new Dictionary<int, bool>();
            var glowAt = new Dictionary<int, bool>();
            foreach (var layer in layers)
            {
                bool parentVisible = layer.Level == 0 || !visibleAt.TryGetValue(layer.Level - 1, out var pv) || pv;
                bool parentGlow = layer.Level > 0 && glowAt.TryGetValue(layer.Level - 1, out var pg) && pg;
                layer.Visible = parentVisible && (layer.Flags & LayerVisible) != 0 && (layer.Flags & LayerReference) == 0;
                layer.IsGlow = parentGlow || layer.Name.IndexOf("glow", StringComparison.OrdinalIgnoreCase) >= 0;
                visibleAt[layer.Level] = layer.Visible;
                glowAt[layer.Level] = layer.IsGlow;
            }
        }

        static string ReadString(BinaryReader r)
        {
            int length = r.ReadUInt16();
            return Encoding.UTF8.GetString(r.ReadBytes(length));
        }

        static void ReadPalette(BinaryReader r, byte[] palette)
        {
            r.ReadUInt32(); // new palette size
            int first = (int)r.ReadUInt32();
            int last = (int)r.ReadUInt32();
            r.ReadBytes(8);
            for (int i = first; i <= last; i++)
            {
                int entryFlags = r.ReadUInt16();
                byte red = r.ReadByte(), green = r.ReadByte(), blue = r.ReadByte(), alpha = r.ReadByte();
                if ((entryFlags & 1) != 0) ReadString(r);
                if (i < 0 || i > 255) continue;
                palette[i * 4] = red; palette[i * 4 + 1] = green; palette[i * 4 + 2] = blue; palette[i * 4 + 3] = alpha;
            }
        }

        static void ReadOldPalette(BinaryReader r, byte[] palette)
        {
            int packets = r.ReadUInt16();
            int index = 0;
            for (int p = 0; p < packets; p++)
            {
                index += r.ReadByte();
                int count = r.ReadByte();
                if (count == 0) count = 256;
                for (int i = 0; i < count; i++, index++)
                {
                    byte red = r.ReadByte(), green = r.ReadByte(), blue = r.ReadByte();
                    if (index > 255) continue;
                    palette[index * 4] = red; palette[index * 4 + 1] = green; palette[index * 4 + 2] = blue; palette[index * 4 + 3] = 255;
                }
            }
        }

        static LayerInfo ReadLayer(BinaryReader r, bool opacityValid)
        {
            var layer = new LayerInfo
            {
                Flags = r.ReadUInt16(),
                Type = r.ReadUInt16(),
                Level = r.ReadUInt16(),
            };
            r.ReadUInt16(); r.ReadUInt16(); // default width and height, ignored
            layer.Blend = r.ReadUInt16();
            int opacity = r.ReadByte();
            layer.Opacity = opacityValid ? opacity : 255;
            r.ReadBytes(3);
            layer.Name = ReadString(r);
            return layer;
        }

        CelInfo ReadCel(BinaryReader r, long chunkEnd, int depth, byte[] palette, int transparentIndex, List<LayerInfo> layers)
        {
            var cel = new CelInfo
            {
                Layer = r.ReadUInt16(),
                X = r.ReadInt16(),
                Y = r.ReadInt16(),
                Opacity = r.ReadByte(),
            };
            int celType = r.ReadUInt16();
            cel.ZIndex = r.ReadInt16();
            r.ReadBytes(5);
            if (cel.Layer >= layers.Count) return null;
            if (celType == 1) return null; // linked cel: frame 0 has nothing earlier to link to
            if (celType == 3)
            {
                Warnings.Add($"Tilemap layer \"{layers[cel.Layer].Name}\" is skipped. Draw tiles on a normal layer instead.");
                return null;
            }
            if (celType != 0 && celType != 2) return null;

            cel.Width = r.ReadUInt16();
            cel.Height = r.ReadUInt16();
            int bytesPerPixel = depth / 8;
            int pixels = cel.Width * cel.Height;
            byte[] raw;
            if (celType == 0)
            {
                raw = r.ReadBytes(pixels * bytesPerPixel);
            }
            else
            {
                r.ReadBytes(2); // zlib header
                var compressed = r.ReadBytes((int)(chunkEnd - r.BaseStream.Position));
                using (var input = new MemoryStream(compressed))
                using (var inflater = new DeflateStream(input, CompressionMode.Decompress))
                using (var output = new MemoryStream())
                {
                    inflater.CopyTo(output);
                    raw = output.ToArray();
                }
            }
            if (raw.Length < pixels * bytesPerPixel) throw new InvalidDataException("A cel is shorter than its size.");

            bool background = (layers[cel.Layer].Flags & LayerBackground) != 0;
            var rgba = new byte[pixels * 4];
            for (int i = 0; i < pixels; i++)
            {
                int o = i * 4;
                if (depth == 32)
                {
                    rgba[o] = raw[o]; rgba[o + 1] = raw[o + 1]; rgba[o + 2] = raw[o + 2]; rgba[o + 3] = raw[o + 3];
                }
                else if (depth == 16)
                {
                    byte v = raw[i * 2];
                    rgba[o] = v; rgba[o + 1] = v; rgba[o + 2] = v; rgba[o + 3] = raw[i * 2 + 1];
                }
                else
                {
                    int index = raw[i];
                    if (index == transparentIndex && !background) continue;
                    rgba[o] = palette[index * 4]; rgba[o + 1] = palette[index * 4 + 1];
                    rgba[o + 2] = palette[index * 4 + 2]; rgba[o + 3] = palette[index * 4 + 3];
                }
            }
            cel.Rgba = rgba;
            return cel;
        }

        void ReadSlice(BinaryReader r)
        {
            uint keys = r.ReadUInt32();
            uint sliceFlags = r.ReadUInt32();
            r.ReadUInt32();
            string name = ReadString(r);
            SheetSlice? chosen = null;
            uint chosenFrame = 0;
            for (uint k = 0; k < keys; k++)
            {
                uint frame = r.ReadUInt32();
                var slice = new SheetSlice
                {
                    Name = name,
                    X = r.ReadInt32(),
                    Y = r.ReadInt32(),
                    Width = (int)r.ReadUInt32(),
                    Height = (int)r.ReadUInt32(),
                };
                if ((sliceFlags & 1) != 0) r.ReadBytes(16); // nine-slice center, unused
                if ((sliceFlags & 2) != 0)
                {
                    slice.HasPivot = true;
                    slice.PivotX = r.ReadInt32();
                    slice.PivotY = r.ReadInt32();
                }
                // Use the key that applies to frame 0: the first key, or a later one that also starts at frame 0.
                if (chosen == null || frame <= chosenFrame) { chosen = slice; chosenFrame = frame; }
            }
            if (chosen.HasValue && chosen.Value.Width > 0 && chosen.Value.Height > 0) Slices.Add(chosen.Value);
        }

        void Composite(double[] target, CelInfo cel, int layerOpacity)
        {
            int x0 = Math.Max(0, cel.X), y0 = Math.Max(0, cel.Y);
            int x1 = Math.Min(Width, cel.X + cel.Width), y1 = Math.Min(Height, cel.Y + cel.Height);
            for (int y = y0; y < y1; y++)
            {
                for (int x = x0; x < x1; x++)
                {
                    int s = ((y - cel.Y) * cel.Width + (x - cel.X)) * 4;
                    double sa = cel.Rgba[s + 3] / 255.0 * (layerOpacity / 255.0) * (cel.Opacity / 255.0);
                    int d = (y * Width + x) * 4;
                    double da = target[d + 3] / 255.0;
                    double oa = sa + da * (1 - sa);
                    for (int ch = 0; ch < 3; ch++)
                        target[d + ch] = oa > 0 ? (cel.Rgba[s + ch] * sa + target[d + ch] * da * (1 - sa)) / oa : 0;
                    target[d + 3] = oa * 255;
                }
            }
        }

        static byte[] ToBytes(double[] values)
        {
            var bytes = new byte[values.Length];
            for (int i = 0; i < values.Length; i++)
                bytes[i] = (byte)Math.Max(0, Math.Min(255, Math.Round(values[i])));
            return bytes;
        }

        /// <summary>True when any pixel in the rectangle (top-left origin) has alpha.</summary>
        public static bool AnyAlpha(byte[] rgba, int width, int x, int y, int w, int h)
        {
            for (int yy = y; yy < y + h; yy++)
                for (int xx = x; xx < x + w; xx++)
                    if (rgba[(yy * width + xx) * 4 + 3] != 0) return true;
            return false;
        }
    }
}
