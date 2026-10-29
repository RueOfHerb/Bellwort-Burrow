using System;
using UnityEngine;

namespace BellwortBurrow.Systems.World
{
    /// <summary>
    /// How high the ground stands under a zone, worked out from its bank and cliff faces (lvl-5). A face is one level
    /// up going up the screen; open ground between faces is smooth, so where a bank ends in open grass its step fades
    /// into a gentle rise around the end. Faces climb from the level below at their foot to the level above at their
    /// lip, read pixel by pixel from the face's own art.
    /// Worked out in the editor (Bellwort Burrow > Tiles > Work Out Heights, and again whenever a Banks layer changes)
    /// and saved here; the Tree Shadow Clock reads it so the log's shadow steps at banks and runs down cliff faces.
    /// </summary>
    [CreateAssetMenu(menuName = "Bellwort Burrow/Terrain Heights", fileName = "TerrainHeights")]
    public sealed class TerrainHeights : ScriptableObject
    {
        /// <summary>A level is a face's height on screen: three rows of 16 px.</summary>
        public const int LevelPixels = 48;
        /// <summary>No face pixel in this column of a face.</summary>
        public const byte NoFace = 255;

        [Tooltip("The lower left cell of the area these heights cover (the clock's ground tilemap's bounds).")]
        public Vector2Int origin;
        public int width, height;
        [Tooltip("Pixels to a tile.")]
        public int tilePixels = 16;
        [Tooltip("Goes up each time the heights are worked out, so the clock knows to reload them.")]
        public int version;
        [Tooltip("Each cell's level, row by row from the bottom. Face cells hold NaN: their heights come from the faces below.")]
        public float[] cells = Array.Empty<float>();

        [Header("Faces, one per column of face cells")]
        public int[] faceX = Array.Empty<int>();
        public int[] faceBottom = Array.Empty<int>();     // first face cell (row)
        public int[] faceTop = Array.Empty<int>();        // one past the last face cell
        public float[] faceLow = Array.Empty<float>();    // the level at its foot
        public float[] faceHigh = Array.Empty<float>();   // the level at its lip
        [Tooltip("For each face, tilePixels pairs (foot, lip) per pixel column, in pixels up from the face's first row.")]
        public byte[] faceEdges = Array.Empty<byte>();

        public int FaceCount => faceX.Length;

        /// <summary>The level of a cell (NaN on a face, or outside).</summary>
        public float CellLevel(int x, int y)
        {
            x -= origin.x; y -= origin.y;
            if (x < 0 || y < 0 || x >= width || y >= height) return float.NaN;
            return cells[y * width + x];
        }

        /// <summary>
        /// The ground's level at every pixel of the area, row by row from the bottom: open ground blends between cell
        /// middles (leaving faces out), and each face climbs from its foot to its lip.
        /// </summary>
        public float[] PixelLevels()
        {
            int p = tilePixels, pw = width * p, ph = height * p;
            var z = new float[pw * ph];
            for (int y = 0; y < ph; y++)
            {
                float cy = (y + 0.5f) / p - 0.5f;
                int y0 = Mathf.FloorToInt(cy);
                float ty = cy - y0;
                for (int x = 0; x < pw; x++)
                {
                    float cx = (x + 0.5f) / p - 0.5f;
                    int x0 = Mathf.FloorToInt(cx);
                    float tx = cx - x0;
                    float sum = 0f, w = 0f;
                    Add(x0, y0, (1 - tx) * (1 - ty));
                    Add(x0 + 1, y0, tx * (1 - ty));
                    Add(x0, y0 + 1, (1 - tx) * ty);
                    Add(x0 + 1, y0 + 1, tx * ty);
                    if (w <= 0f)
                    {
                        float near = At(x / p, y / p);
                        z[y * pw + x] = float.IsNaN(near) ? 0f : near;
                    }
                    else z[y * pw + x] = sum / w;

                    void Add(int cxi, int cyi, float weight)
                    {
                        float v = At(cxi, cyi);
                        if (float.IsNaN(v) || weight <= 0f) return;
                        sum += v * weight;
                        w += weight;
                    }
                }
            }

            for (int f = 0; f < FaceCount; f++)
            {
                int rowFrom = faceBottom[f] * p, rowTo = faceTop[f] * p;
                for (int i = 0; i < p; i++)
                {
                    int x = faceX[f] * p + i;
                    byte foot = faceEdges[(f * p + i) * 2], lip = faceEdges[(f * p + i) * 2 + 1];
                    float from = foot == NoFace ? rowFrom : rowFrom + foot;
                    float to = lip == NoFace ? rowTo : rowFrom + lip;
                    if (to <= from) to = from + 1;
                    for (int y = rowFrom; y < rowTo; y++)
                    {
                        float t = Mathf.Clamp01((y + 0.5f - from) / (to - from));
                        z[y * pw + x] = Mathf.Lerp(faceLow[f], faceHigh[f], t);
                    }
                }
            }
            return z;

            float At(int cx, int cy) =>
                cx < 0 || cy < 0 || cx >= width || cy >= height ? float.NaN : cells[cy * width + cx];
        }

        /// <summary>
        /// Works out each open cell's level from where the faces are (face[x, y] true on a face cell): one level up
        /// across a face going up the screen, level between neighbours elsewhere, as near as both can be had (least
        /// squares, the faces counting far more). The lowest ground is level 0. Face cells come back NaN. start, the
        /// last heights for the same cells, makes a small change settle quickly.
        /// </summary>
        public static float[] Solve(bool[,] face, float[] start = null, int iterations = 6000, float faceWeight = 30f,
                                    float tolerance = 2e-5f)
        {
            int w = face.GetLength(0), h = face.GetLength(1), n = w * h;
            var level = new float[n];
            if (start != null && start.Length == n)
            {
                // Start from the last heights (a small edit then settles in a few passes); new open cells start level
                // with an open neighbour.
                for (int i = 0; i < n; i++) level[i] = float.IsNaN(start[i]) ? 0f : start[i];
            }
            else
            {
                // A first guess: faces counted up each column.
                for (int x = 0; x < w; x++)
                {
                    int count = 0;
                    for (int y = 0; y < h; y++)
                    {
                        if (face[x, y]) { if (y + 1 < h && !face[x, y + 1]) count++; continue; }
                        level[y * w + x] = count;
                    }
                }
            }

            // Links, packed per cell: the other cell, its weight and how much higher this cell should be than it.
            var count4 = new int[n + 1];
            var pairs = new System.Collections.Generic.List<(int a, int b, float weight, float rise)>();
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (face[x, y]) continue;
                if (x + 1 < w && !face[x + 1, y]) pairs.Add((y * w + x + 1, y * w + x, 1f, 0f));
                if (y + 1 < h && !face[x, y + 1]) pairs.Add(((y + 1) * w + x, y * w + x, 1f, 0f));
            }
            for (int x = 0; x < w; x++)
            {
                int y = 0;
                while (y < h)
                {
                    if (!face[x, y]) { y++; continue; }
                    int bottom = y;
                    while (y < h && face[x, y]) y++;
                    if (bottom > 0 && y < h) pairs.Add((y * w + x, (bottom - 1) * w + x, faceWeight, 1f));
                }
            }
            foreach (var (pa, pb, _, _) in pairs) { count4[pa + 1]++; count4[pb + 1]++; }
            for (int i = 0; i < n; i++) count4[i + 1] += count4[i];
            int links = count4[n];
            var other = new int[links];
            var weight = new float[links];
            var rise = new float[links];
            var fill = (int[])count4.Clone();
            foreach (var (pa, pb, wt, r) in pairs)
            {
                int k = fill[pa]++; other[k] = pb; weight[k] = wt; rise[k] = r;
                k = fill[pb]++; other[k] = pa; weight[k] = wt; rise[k] = -r;
            }
            var total = new float[n];
            for (int i = 0; i < n; i++)
                for (int k = count4[i]; k < count4[i + 1]; k++) total[i] += weight[k];

            const float omega = 1.9f;
            for (int it = 0; it < iterations; it++)
            {
                float most = 0f;
                for (int i = 0; i < n; i++)
                {
                    if (total[i] <= 0f) continue;
                    float sum = 0f;
                    for (int k = count4[i]; k < count4[i + 1]; k++) sum += weight[k] * (level[other[k]] + rise[k]);
                    float step = omega * (sum / total[i] - level[i]);
                    level[i] += step;
                    if (step > most) most = step; else if (-step > most) most = -step;
                }
                if (most < tolerance) break;
            }

            float lowest = float.PositiveInfinity;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (!face[x, y]) lowest = Mathf.Min(lowest, level[y * w + x]);
            if (float.IsInfinity(lowest)) lowest = 0f;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                level[y * w + x] = face[x, y] ? float.NaN : level[y * w + x] - lowest;
            return level;
        }
    }
}
