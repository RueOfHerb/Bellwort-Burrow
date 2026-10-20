using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace BellwortBurrow.EditorTools
{
    /// <summary>
    /// Builds the "Forest Ground" Tile Palette (Window > 2D > Tile Palette) from forest_ground.aseprite.
    /// The top row holds the tiles you paint with (grass, path, half path, deer trail; see <see cref="GroundAutoTiles"/>).
    /// Below them, each ground set painted from <see cref="GroundAutoTiles.Template"/> (Forest Path, then the deer trail
    /// beside it) is laid out as ready-made shapes (<see cref="Groups"/>) for painting by hand: every piece sits beside
    /// the pieces it joins, and every layout comes up at least once. Under the shapes wait the set's other pieces (the
    /// repeats and fills the tile picks from at random), in a run per layout. Every other slice (grass, tufts, crops,
    /// the half path set) goes to the right, as it sits on the sheet. The sheet itself keeps its patches, because the
    /// tiles read each piece's layout from its spot there.
    /// If you rearranged the palette by hand, it is left alone (see <see cref="GeneratedAssets"/>).
    /// </summary>
    public static class ForestGroundPalette
    {
        const string SheetName = "forest_ground";
        const string Folder = ArtLibrary.ArtRoot + "/Tile Palettes";
        const string PaletteName = "Forest Ground";
        public const string PalettePath = Folder + "/" + PaletteName + ".prefab";

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

        [MenuItem("Bellwort Burrow/Setup/Forest Steps/5. Make Ground Tile Palette", false, 205)]
        static void MakeFromMenu()
        {
            if (!ArtLibrary.CheckArtInstalled()) return;
            if (Make()) Debug.Log($"Bellwort Burrow: painted the {PaletteName} tile palette at {PalettePath}.");
        }

        public static bool Make()
        {
            string sheetPath = ArtLibrary.FindSheet(SheetName);
            if (sheetPath == null)
            {
                Debug.LogError($"Bellwort Burrow: couldn't find {SheetName}.aseprite under {ArtLibrary.ArtRoot}.");
                return false;
            }
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
            if (!GeneratedAssets.CanOverwrite(PalettePath))
            {
                GeneratedAssets.ReportKept("Make Ground Tile Palette", new List<string> { PalettePath });
                return true;
            }
            var palette = AssetDatabase.LoadAssetAtPath<GameObject>(PalettePath);
            if (palette == null)
            {
                palette = GridPaletteUtility.CreateNewPalette(Folder, PaletteName, GridLayout.CellLayout.Rectangle,
                    GridPalette.CellSizing.Automatic, Vector3.one, GridLayout.CellSwizzle.XYZ);
            }

            // An open Tile Palette window showing this palette writes its own copy back over the file when the file
            // is reimported, so while it's rebuilt the window shows a stand-in palette, and this one again after.
            GameObject standIn = null;
            if (GridPaintingState.palette != null && AssetDatabase.GetAssetPath(GridPaintingState.palette) == PalettePath)
            {
                standIn = GridPaletteUtility.CreateNewPalette(Folder, PaletteName + " (rebuilding)", GridLayout.CellLayout.Rectangle,
                    GridPalette.CellSizing.Automatic, Vector3.one, GridLayout.CellSwizzle.XYZ);
                GridPaintingState.palette = standIn;
            }
            try
            {
                Fill(palette, sheet, tiles, tileSize);
            }
            finally
            {
                if (standIn != null)
                {
                    GridPaintingState.palette = AssetDatabase.LoadAssetAtPath<GameObject>(PalettePath);
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

        static void Fill(GameObject palette, AsepriteSheet sheet, Dictionary<string, TileBase> tiles, int tileSize)
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

            // The ground sets, side by side: shapes, with the other pieces under them.
            int left = 1;
            foreach (var (slice, fills) in GroundAutoTiles.TemplateSets())
            {
                foreach (var (column, row, piece) in SetSpots(slice, fills)) Put(left + column, row, piece);
                left += SetWidth + SetGap;
            }

            // Every other slice to the right, as it sits on the sheet. Slices that share rows on the sheet share a band,
            // side by side; the bands go one under the other.
            var rest = sheet.Slices
                .Where(s => SliceTiles(s, tileSize).Any(t => tiles.ContainsKey(t.name) && !placed.Contains(t.name)))
                .OrderBy(s => s.Y).ThenBy(s => s.X).ToList();
            int bandTop = 0;
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
                bandTop += (sheetBottom - sheetTop) / tileSize + GroupGap;
                i = end;
            }

            // The painting tiles, in a row above.
            int paintColumn = 1;
            foreach (var path in GroundAutoTiles.TilePaths())
            {
                var paintTile = AssetDatabase.LoadAssetAtPath<TileBase>(path);
                if (paintTile == null) continue;
                tilemap.SetTile(new Vector3Int(paintColumn, 1, 0), paintTile);
                paintColumn += 2;
            }
            tilemap.CompressBounds();

            PrefabUtility.SaveAsPrefabAssetAndConnect(instance, PalettePath, InteractionMode.AutomatedAction);
            Object.DestroyImmediate(instance);
            AssetDatabase.SaveAssets();
            GeneratedAssets.Stamp(PalettePath);
        }
    }
}
