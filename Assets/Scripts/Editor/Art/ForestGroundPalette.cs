using System;
using System.IO;
using UnityEditor;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace BellwortBurrow.EditorTools
{
    /// <summary>
    /// Builds the "Forest Ground" Tile Palette (Window > 2D > Tile Palette) from forest_ground.aseprite.
    /// The top row holds the tiles you paint with (grass, path, deer trail; see <see cref="GroundAutoTiles"/>).
    /// Below them, every single piece sits where it sits on the sheet, so that part looks like the Aseprite file.
    /// If you rearranged the palette by hand, it is left alone (see <see cref="GeneratedAssets"/>).
    /// </summary>
    public static class ForestGroundPalette
    {
        const string SheetName = "forest_ground";
        const string Folder = ArtLibrary.ArtRoot + "/Tile Palettes";
        const string PaletteName = "Forest Ground";
        public const string PalettePath = Folder + "/" + PaletteName + ".prefab";

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
                GeneratedAssets.ReportKept("Make Ground Tile Palette", new System.Collections.Generic.List<string> { PalettePath });
                return true;
            }
            var palette = AssetDatabase.LoadAssetAtPath<GameObject>(PalettePath);
            if (palette == null)
            {
                palette = GridPaletteUtility.CreateNewPalette(Folder, PaletteName, GridLayout.CellLayout.Rectangle,
                    GridPalette.CellSizing.Automatic, Vector3.one, GridLayout.CellSwizzle.XYZ);
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(palette);
            var tilemap = instance.GetComponentInChildren<Tilemap>();
            tilemap.ClearAllTiles();

            foreach (var slice in sheet.Slices)
            {
                int columns = slice.Width / tileSize, rows = slice.Height / tileSize;
                string digits = new string('0', Mathf.Max(2, (columns * rows).ToString().Length));
                for (int row = 0; row < rows; row++)
                {
                    for (int column = 0; column < columns; column++)
                    {
                        int index = row * columns + column;
                        string name = columns * rows == 1 ? slice.Name : slice.Name + "_" + index.ToString(digits);
                        if (!tiles.TryGetValue(name, out var tile)) continue; // blank tiles are skipped on import
                        var cell = new Vector3Int(slice.X / tileSize + column, -(slice.Y / tileSize + row), 0);
                        tilemap.SetTile(cell, tile);
                    }
                }
            }
            // The painting tiles, in a row above the sheet.
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
            return true;
        }
    }
}
