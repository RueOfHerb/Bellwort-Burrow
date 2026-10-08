using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace BellwortBurrow.EditorTools
{
    /// <summary>
    /// Reads the Glow layer of a slice and turns its bright spots into 2D point lights, so lit windows,
    /// wisps and lantern flies light the ground around them instead of only glowing themselves.
    ///
    /// - A sprite that is mostly glow (a wisp, a lantern fly) gets one light in its own color.
    /// - Warm spots (window glass) get the lantern fly yellow; windows close together share one light.
    /// - Other bigger spots (the ruby door sigil, garnet crystals) get a small light in their own color.
    /// - Tiny glints (dew, gem sparkles) glow without casting light.
    /// </summary>
    public static class GlowLights
    {
        // Lantern fly base, #FAD957: the warm light every lit window casts.
        static readonly Color WindowColor = new Color32(250, 217, 87, 255);
        const float WindowRadius = 70f / 16f;
        const float WindowIntensity = 0.85f;
        const float SpotRadius = 1.5f;
        const float SpotIntensity = 0.5f;

        const int JoinDistance = 2;        // glow pixels this close are one spot
        const int WindowGroupDistance = 24; // windows this close (in pixels) share a light
        const int SmallestWindow = 4;
        const int SmallestLitSpot = 10;
        const float LightSourceShare = 0.3f; // glow covering this share of the art makes the sprite a light source

        public struct Spot
        {
            public string Name;
            public Vector2 Position; // local, in units, from the sprite's pivot
            public Color Color;
            public float Radius;
            public float Intensity;
        }

        struct Blob
        {
            public int Count;
            public float SumX, SumY, SumR, SumG, SumB;
            public Vector2 Center => new Vector2(SumX / Count, SumY / Count);
            public Color Mean => new Color(SumR / Count, SumG / Count, SumB / Count);
        }

        public static List<Spot> Find(AsepriteSheet sheet, SheetSlice slice)
        {
            var spots = new List<Spot>();
            if (sheet == null || !sheet.HasGlow) return spots;

            int width = slice.Width, height = slice.Height;
            float pivotX = slice.HasPivot ? slice.PivotX : width / 2f;
            float pivotY = slice.HasPivot ? slice.PivotY : height;
            Vector2 ToLocal(Vector2 pixel) => new Vector2(
                (pixel.x + 0.5f - pivotX) / ArtLibrary.PixelsPerUnit,
                (pivotY - pixel.y - 0.5f) / ArtLibrary.PixelsPerUnit);

            bool IsGlow(int x, int y) =>
                x >= 0 && y >= 0 && x < width && y < height &&
                slice.X + x < sheet.Width && slice.Y + y < sheet.Height &&
                sheet.Glow[((slice.Y + y) * sheet.Width + slice.X + x) * 4 + 3] > 0;

            int artPixels = 0;
            for (int y = 0; y < height && slice.Y + y < sheet.Height; y++)
                for (int x = 0; x < width && slice.X + x < sheet.Width; x++)
                    if (sheet.Art[((slice.Y + y) * sheet.Width + slice.X + x) * 4 + 3] > 0) artPixels++;

            // Flood fill the glow pixels into blobs.
            var seen = new bool[width * height];
            var blobs = new List<Blob>();
            var queue = new Queue<Vector2Int>();
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (seen[y * width + x] || !IsGlow(x, y)) continue;
                    var blob = new Blob();
                    seen[y * width + x] = true;
                    queue.Enqueue(new Vector2Int(x, y));
                    while (queue.Count > 0)
                    {
                        var p = queue.Dequeue();
                        int i = ((slice.Y + p.y) * sheet.Width + slice.X + p.x) * 4;
                        blob.Count++;
                        blob.SumX += p.x; blob.SumY += p.y;
                        blob.SumR += sheet.Glow[i] / 255f;
                        blob.SumG += sheet.Glow[i + 1] / 255f;
                        blob.SumB += sheet.Glow[i + 2] / 255f;
                        for (int dy = -JoinDistance; dy <= JoinDistance; dy++)
                        {
                            for (int dx = -JoinDistance; dx <= JoinDistance; dx++)
                            {
                                int nx = p.x + dx, ny = p.y + dy;
                                if (!IsGlow(nx, ny) || seen[ny * width + nx]) continue;
                                seen[ny * width + nx] = true;
                                queue.Enqueue(new Vector2Int(nx, ny));
                            }
                        }
                    }
                    blobs.Add(blob);
                }
            }
            if (blobs.Count == 0) return spots;

            // Mostly glow: the whole sprite is a light source.
            int glowPixels = 0;
            foreach (var blob in blobs) glowPixels += blob.Count;
            if (glowPixels >= artPixels * LightSourceShare)
            {
                var all = new Blob();
                foreach (var blob in blobs)
                {
                    all.Count += blob.Count;
                    all.SumX += blob.SumX; all.SumY += blob.SumY;
                    all.SumR += blob.SumR; all.SumG += blob.SumG; all.SumB += blob.SumB;
                }
                spots.Add(new Spot
                {
                    Name = "Light",
                    Position = ToLocal(all.Center),
                    Color = Brighten(all.Mean),
                    Radius = Mathf.Clamp(Mathf.Sqrt(glowPixels) * 0.45f, 1f, 4f),
                    Intensity = 0.7f,
                });
                return spots;
            }

            // Windows: warm blobs, grouped when close.
            var windows = new List<Blob>();
            foreach (var blob in blobs)
            {
                if (blob.Count >= SmallestWindow && IsWarm(blob.Mean))
                {
                    int group = windows.FindIndex(w => Vector2.Distance(w.Center, blob.Center) < WindowGroupDistance);
                    if (group < 0) { windows.Add(blob); continue; }
                    var merged = windows[group];
                    merged.Count += blob.Count;
                    merged.SumX += blob.SumX; merged.SumY += blob.SumY;
                    merged.SumR += blob.SumR; merged.SumG += blob.SumG; merged.SumB += blob.SumB;
                    windows[group] = merged;
                }
                else if (blob.Count >= SmallestLitSpot)
                {
                    spots.Add(new Spot
                    {
                        Name = "Glow Light",
                        Position = ToLocal(blob.Center),
                        Color = Brighten(blob.Mean),
                        Radius = SpotRadius,
                        Intensity = SpotIntensity,
                    });
                }
            }
            foreach (var window in windows)
            {
                spots.Add(new Spot
                {
                    Name = "Window Light",
                    Position = ToLocal(window.Center),
                    Color = WindowColor,
                    Radius = WindowRadius,
                    Intensity = WindowIntensity,
                });
            }
            return spots;
        }

        /// <summary>
        /// Adds a point light child for each spot. Each child gets its own name ("Window Light", "Window Light 2"...),
        /// because Unity matches children by name when setup rebuilds a prefab: unique names keep each light the same
        /// object across rebuilds, so changes made to it in a scene keep applying.
        /// </summary>
        public static void AddLights(GameObject root, List<Spot> spots)
        {
            var layers = AllSortingLayers();
            var used = new Dictionary<string, int>();
            foreach (var spot in spots)
            {
                used.TryGetValue(spot.Name, out int count);
                used[spot.Name] = ++count;
                var lightObject = new GameObject(count == 1 ? spot.Name : $"{spot.Name} {count}");
                lightObject.transform.SetParent(root.transform, false);
                lightObject.transform.localPosition = spot.Position;
                var light = lightObject.AddComponent<Light2D>();
                light.lightType = Light2D.LightType.Point;
                light.color = spot.Color;
                light.intensity = spot.Intensity;
                light.pointLightOuterRadius = spot.Radius;
                light.pointLightInnerRadius = spot.Radius * 0.15f;
                light.falloffIntensity = 0.65f;
                light.shadowsEnabled = false;
                light.targetSortingLayers = layers;
            }
        }

        /// <summary>Every sorting layer, so a light reaches whatever it shines on.</summary>
        public static int[] AllSortingLayers()
        {
            var layers = SortingLayer.layers;
            var ids = new int[layers.Length];
            for (int i = 0; i < layers.Length; i++) ids[i] = layers[i].id;
            return ids;
        }

        static bool IsWarm(Color color)
        {
            Color.RGBToHSV(color, out float hue, out float saturation, out _);
            return hue >= 0.06f && hue <= 0.19f && saturation > 0.3f;
        }

        static Color Brighten(Color color)
        {
            float max = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
            return max > 0f ? new Color(color.r / max, color.g / max, color.b / max, 1f) : Color.white;
        }
    }
}
