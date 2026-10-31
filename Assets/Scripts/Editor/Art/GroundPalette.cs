using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BellwortBurrow.Tiles;
using UnityEditor;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace BellwortBurrow.EditorTools
{
    /// <summary>
    /// Builds each zone's ground Tile Palette (Window > 2D > Tile Palette) from its ground sheet: "Forest Ground" from
    /// forest_ground.aseprite, "Burrow Ground" from burrow_ground.aseprite (see <see cref="GroundAutoTiles.Sheets"/>).
    /// The top row holds the tiles you paint with (grass, path, deer trail, and on a sheet with stone, water or raised
    /// ground the flagstones, water, faces, steps and ramps; see <see cref="GroundAutoTiles"/>). The raised ground's
    /// sets sit with the other slices, as painted on the sheet. Below them, each ground set painted from <see cref="GroundAutoTiles.Template"/>
    /// (the path, then the deer trail beside it) is laid out as ready-made shapes (<see cref="Groups"/>) for painting by
    /// hand: every piece sits beside the pieces it joins, and every layout comes up at least once. Every other slice
    /// (grass, tufts, crops) goes to the right of the sets, as it sits on the sheet. The set's other pieces (the repeats
    /// and fills the tile picks from at random, in a run per layout) wait in a column of their own at the far right, one
    /// per set, so the palette opens on the shapes (Jocelyn, Oct 20). The sheet itself keeps its patches, because the
    /// tiles read each piece's layout from its spot there. (Until Oct 24 each set had a half tile with shapes of its own
    /// under these; the half tiles were cut.)
    /// If you rearranged a palette by hand, it is left alone (see <see cref="GeneratedAssets"/>).
    /// </summary>
    public static class GroundPalette
    {
        const string Folder = ArtLibrary.ArtRoot + "/Tile Palettes";

        /// <summary>Where a ground sheet's tile palette is saved.</summary>
        public static string PalettePath(GroundAutoTiles.GroundSheet ground) => Folder + "/" + ground.Palette + ".prefab";

        /// <summary>How many columns a set's shapes take before wrapping to the next row.</summary>
        internal const int SetWidth = 24;
        // Empty columns between sets, and empty rows between groups of shapes (one between rows of a group).
        const int SetGap = 3, GroupGap = 2;

        /// <summary>
        /// The shapes a ground set is laid out in, group by group, top row first: '#' painted, '.' not. A shape is only
        /// laid out when it brings a layout the shapes before it don't have. The names are only for reading here: the
        /// Tile Palette window can't show text.
        /// </summary>
        internal static readonly (string name, string[][] shapes)[] Groups =
        {
            ("Wide path", new[] { Shape("###", "###", "###"), Shape("#####", "#####", "##.##", "#####", "#####") }),
            ("One tile wide", new[] { Shape("#"), Shape("####"), Shape("#", "#", "#", "#"), Shape("###", "#.#", "###"), Shape(".#.", "###", ".#.") }
                .Concat(Turns("###", ".#.")).ToArray()),
            ("Wide meets narrow", Turns("..#..", "#####", "#####", "#####").Concat(Turns("##..", "####", "####", "##..")).ToArray()),
            ("Diagonals", new[]
            {
                Shape("..#..", ".###.", "#####", ".###.", "..#.."), Shape("##..", ".##.", "..##"), Shape("..##", ".##.", "##.."),
                Shape("###..", "####.", ".####", "..###"), Shape("..###", ".####", "####.", "###.."),
            }),
            ("Inner corners", Turns(".#.", "###", ".##").Concat(Turns("##.", "###", ".##").Take(2))
                .Concat(Turns("###", ".##")).Concat(Turns("###", "##.")).ToArray()),
            ("Bends into slopes", Turns(".#", "##", "#.").Concat(new[] { Shape(".#.", ".##", "..#"), Shape("#..", "##.", ".#.") }).ToArray()),
        };

        static string[] Shape(params string[] rows) => rows;

        /// <summary>A shape and its quarter turns clockwise, each once.</summary>
        static string[][] Turns(params string[] shape)
        {
            var turns = new List<string[]>();
            var current = shape;
            for (int i = 0; i < 4; i++)
            {
                if (!turns.Any(t => t.SequenceEqual(current))) turns.Add(current);
                current = Turn(current);
            }
            return turns.ToArray();
        }

        static string[] Turn(string[] shape)
        {
            int height = shape.Length, width = shape[0].Length;
            var turned = new string[width];
            for (int column = 0; column < width; column++)
            {
                var row = new char[height];
                for (int r = 0; r < height; r++) row[r] = shape[height - 1 - r][column];
                turned[column] = new string(row);
            }
            return turned;
        }

        /// <summary>
        /// The painted cells of a set's shapes: column and row (down from the set's top left), and the layout the piece
        /// there needs. Shapes sit a cell apart, so none of them changes another's layouts.
        /// </summary>
        internal static List<(int column, int row, uint layout)> ShapeSpots()
        {
            var spots = new List<(int, int, uint)>();
            var seen = new HashSet<uint>();
            int top = 0;
            foreach (var (_, shapes) in Groups)
            {
                int x = 0, y = top, rowHeight = 0;
                foreach (var shape in shapes)
                {
                    var cells = new List<(int column, int row, uint layout)>();
                    for (int row = 0; row < shape.Length; row++)
                        for (int column = 0; column < shape[row].Length; column++)
                            if (shape[row][column] == '#') cells.Add((column, row, GroundAutoTiles.Layout(shape, column, row)));
                    if (cells.All(c => seen.Contains(c.layout))) continue;
                    int width = shape[0].Length;
                    if (x > 0 && x + width > SetWidth) { y += rowHeight + 1; x = 0; rowHeight = 0; }
                    foreach (var c in cells) { spots.Add((x + c.column, y + c.row, c.layout)); seen.Add(c.layout); }
                    x += width + 1;
                    rowHeight = Math.Max(rowHeight, shape.Length);
                }
                if (rowHeight > 0) top = y + rowHeight + GroupGap;
            }
            return spots;
        }

        /// <summary>
        /// Where a ground set's pieces go: column and row (down from the set's top left), and the piece. The shapes take
        /// the first piece of each layout on the sheet. Under them, the set's other pieces wait in runs, one run per
        /// layout, the full layout's run ending with the fills.
        /// </summary>
        internal static List<(int column, int row, string piece)> SetSpots(string slice, string[] fills)
        {
            var template = GroundAutoTiles.Template;
            var pieces = new Dictionary<uint, List<string>>();
            var order = new List<uint>();
            for (int row = 0; row < template.Length; row++)
            {
                for (int column = 0; column < template[row].Length; column++)
                {
                    if (template[row][column] != '#') continue;
                    uint layout = GroundAutoTiles.Layout(template, column, row);
                    if (!pieces.TryGetValue(layout, out var list)) { pieces[layout] = list = new List<string>(); order.Add(layout); }
                    list.Add(GroundAutoTiles.PieceName(slice, column, row, template));
                }
            }
            if (pieces.TryGetValue(GroundAutoTiles.Full, out var full)) full.AddRange(fills);

            var spots = new List<(int, int, string)>();
            var shapes = ShapeSpots();
            var shaped = new List<uint>();
            foreach (var (column, row, layout) in shapes)
            {
                if (!pieces.TryGetValue(layout, out var list)) continue;
                spots.Add((column, row, list[0]));
                if (!shaped.Contains(layout)) shaped.Add(layout);
            }
            // The runs follow the order the shapes first use each layout; a layout no shape uses comes last, whole.
            int x = 0, y = shapes.Count == 0 ? 0 : shapes.Max(s => s.row) + 1 + GroupGap;
            foreach (var layout in shaped.Concat(order.Where(l => !shaped.Contains(l))))
            {
                var run = pieces[layout].Skip(shaped.Contains(layout) ? 1 : 0).ToList();
                if (run.Count == 0) continue;
                if (x > 0 && x + run.Count > SetWidth) { y += 2; x = 0; }
                foreach (var piece in run) spots.Add((x++, y, piece));
                x++;
            }
            return spots;
        }

        /// <summary>
        /// Splits a set's spots (<see cref="SetSpots"/>) into its shapes, the spots down to
        /// the shapes' last row, and its runs, the spots under them, moved up so the first run sits on row 0.
        /// </summary>
        internal static (List<(int column, int row, string piece)> shapes, List<(int column, int row, string piece)> runs)
            SplitRuns(List<(int column, int row, string piece)> spots, int shapesBottom)
        {
            var shapes = spots.Where(s => s.row <= shapesBottom).ToList();
            var runs = spots.Where(s => s.row > shapesBottom).ToList();
            if (runs.Count > 0)
            {
                int top = runs.Min(s => s.row);
                runs = runs.Select(s => (s.column, s.row - top, s.piece)).ToList();
            }
            return (shapes, runs);
        }

        [MenuItem("Bellwort Burrow/Setup/Forest Steps/5. Make Ground Tile Palettes", false, 205)]
        static void MakeFromMenu()
        {
            if (!ArtLibrary.CheckArtInstalled()) return;
            var made = MakeAll();
            if (made.Count > 0) Debug.Log($"Bellwort Burrow: painted the {string.Join(" and ", made)} tile palette(s) in {Folder}.");
        }

        /// <summary>
        /// Builds the tile palette of every installed ground sheet. Returns the names of the palettes made, or null
        /// when one couldn't be made.
        /// </summary>
        public static List<string> MakeAll()
        {
            var made = new List<string>();
            foreach (var ground in GroundAutoTiles.Sheets)
            {
                string sheetPath = ArtLibrary.FindSheet(ground.Sheet);
                if (sheetPath == null) continue; // GroundAutoTiles.MakeAll says which sheets are missing
                if (!Make(ground, sheetPath)) return null;
                made.Add(ground.Palette);
            }
            if (made.Count == 0)
            {
                Debug.LogError($"Bellwort Burrow: couldn't find any ground sheet under {ArtLibrary.ArtRoot}.");
                return null;
            }
            return made;
        }

        static bool Make(GroundAutoTiles.GroundSheet ground, string sheetPath)
        {
            string palettePath = PalettePath(ground), paletteName = ground.Palette;
            var importer = (BellwortSheetImporter)AssetImporter.GetAtPath(sheetPath);
            int tileSize = Mathf.Max(1, importer.tileSize);

            AsepriteSheet sheet;
            try { sheet = AsepriteSheet.Read(File.ReadAllBytes(sheetPath)); }
            catch (Exception e)
            {
                Debug.LogError($"Bellwort Burrow: could not read {sheetPath}: {e.Message}");
                return false;
            }
            var tiles = ArtLibrary.LoadSubAssets<TileBase>(sheetPath);

            ArtLibrary.EnsureFolder(Folder);
            if (!GeneratedAssets.CanOverwrite(palettePath))
            {
                GeneratedAssets.ReportKept("Make Ground Tile Palettes", new List<string> { palettePath });
                return true;
            }
            var palette = AssetDatabase.LoadAssetAtPath<GameObject>(palettePath);
            if (palette == null)
            {
                palette = GridPaletteUtility.CreateNewPalette(Folder, paletteName, GridLayout.CellLayout.Rectangle,
                    GridPalette.CellSizing.Automatic, Vector3.one, GridLayout.CellSwizzle.XYZ);
            }

            // An open Tile Palette window showing this palette writes its own copy back over the file when the file
            // is reimported, so while it's rebuilt the window shows a stand-in palette, and this one again after.
            GameObject standIn = null;
            if (GridPaintingState.palette != null && AssetDatabase.GetAssetPath(GridPaintingState.palette) == palettePath)
            {
                standIn = GridPaletteUtility.CreateNewPalette(Folder, paletteName + " (rebuilding)", GridLayout.CellLayout.Rectangle,
                    GridPalette.CellSizing.Automatic, Vector3.one, GridLayout.CellSwizzle.XYZ);
                GridPaintingState.palette = standIn;
            }
            try
            {
                Fill(ground, palette, palettePath, sheet, tiles, tileSize);
            }
            finally
            {
                if (standIn != null)
                {
                    GridPaintingState.palette = AssetDatabase.LoadAssetAtPath<GameObject>(palettePath);
                    AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(standIn));
                }
            }
            return true;
        }

        /// <summary>A slice's tiles: column and row in the slice, and the name the importer gives the tile.</summary>
        static IEnumerable<(int column, int row, string name)> SliceTiles(SheetSlice slice, int tileSize)
        {
            int columns = slice.Width / tileSize, rows = slice.Height / tileSize;
            string digits = new string('0', Mathf.Max(2, (columns * rows).ToString().Length));
            for (int row = 0; row < rows; row++)
                for (int column = 0; column < columns; column++)
                    yield return (column, row, columns * rows == 1 ? slice.Name : slice.Name + "_" + (row * columns + column).ToString(digits));
        }

        static void Fill(GroundAutoTiles.GroundSheet ground, GameObject palette, string palettePath, AsepriteSheet sheet, Dictionary<string, TileBase> tiles, int tileSize)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(palette);
            var tilemap = instance.GetComponentInChildren<Tilemap>();
            tilemap.ClearAllTiles();

            // Rows count down from the first row under the painting tiles.
            var placed = new HashSet<string>();
            void Put(int column, int row, string name)
            {
                if (!tiles.TryGetValue(name, out var tile)) return; // blank tiles are skipped on import
                tilemap.SetTile(new Vector3Int(column, -1 - row, 0), tile);
                placed.Add(name);
            }

            // The ground sets, side by side: each set's shapes. Their other pieces (the runs) wait for a column of their
            // own at the far right, so they count as placed already.
            int left = 1;
            var shapesBottom = ShapeSpots().Select(s => s.row).DefaultIfEmpty(-1).Max();
            var runColumns = new List<List<(int column, int row, string piece)>>();
            foreach (var (slice, fills) in GroundAutoTiles.TemplateSets(ground))
            {
                var (shapes, runs) = SplitRuns(SetSpots(slice, fills), shapesBottom);
                foreach (var (column, row, piece) in shapes) Put(left + column, row, piece);
                foreach (var spot in runs) if (tiles.ContainsKey(spot.piece)) placed.Add(spot.piece);
                runColumns.Add(runs);
                left += SetWidth + SetGap;
            }

            // Every other slice to the right, as it sits on the sheet. Slices that share rows on the sheet share a band,
            // side by side; the bands go one under the other.
            var rest = sheet.Slices
                .Where(s => !GroundAutoTiles.IsAnimationFrameSlice(s.Name))   // frames, not tiles to paint (art-4)
                .Where(s => SliceTiles(s, tileSize).Any(t => tiles.ContainsKey(t.name) && !placed.Contains(t.name)))
                .OrderBy(s => s.Y).ThenBy(s => s.X).ToList();
            int bandTop = 0, restRight = left;
            for (int i = 0; i < rest.Count;)
            {
                int sheetTop = rest[i].Y, sheetBottom = rest[i].Y + rest[i].Height, end = i + 1;
                while (end < rest.Count && rest[end].Y < sheetBottom) { sheetBottom = Math.Max(sheetBottom, rest[end].Y + rest[end].Height); end++; }
                int x = left;
                for (int k = i; k < end; k++)
                {
                    int down = (rest[k].Y - sheetTop) / tileSize;
                    foreach (var (column, row, name) in SliceTiles(rest[k], tileSize)) Put(x + column, bandTop + down + row, name);
                    x += rest[k].Width / tileSize + 1;
                }
                restRight = Math.Max(restRight, x);
                bandTop += (sheetBottom - sheetTop) / tileSize + GroupGap;
                i = end;
            }

            // The runs, a column per set at the far right, in the same order as the sets.
            int runLeft = restRight + SetGap - 1;
            foreach (var column_ in runColumns)
            {
                foreach (var (column, row, piece) in column_) Put(runLeft + column, row, piece);
                runLeft += SetWidth + SetGap;
            }

            // The painting tiles, in a row above; the ramps above that, each as its whole block, to pick up and stamp.
            int paintColumn = 1, rampColumn = 1;
            foreach (var path in GroundAutoTiles.TilePaths(ground))
            {
                var paintTile = AssetDatabase.LoadAssetAtPath<TileBase>(path);
                if (paintTile == null) continue;
                if (paintTile is RampTile ramp)
                {
                    for (int row = 0; row < ramp.rows; row++)
                        for (int column = 0; column < ramp.columns; column++)
                            tilemap.SetTile(new Vector3Int(rampColumn + column, 3 + ramp.rows - 1 - row, 0), paintTile);
                    rampColumn += ramp.columns + 1;
                    continue;
                }
                tilemap.SetTile(new Vector3Int(paintColumn, 1, 0), paintTile);
                paintColumn += 2;
            }
            tilemap.CompressBounds();

            PrefabUtility.SaveAsPrefabAssetAndConnect(instance, palettePath, InteractionMode.AutomatedAction);
            Object.DestroyImmediate(instance);
            AssetDatabase.SaveAssets();
            GeneratedAssets.Stamp(palettePath);
        }
    }
}
