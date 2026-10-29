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
    /// What runs on its own when the terrain changes (arc-11), so nobody has to remember the menu items:
    /// - Saving a ground sheet (each one in <see cref="GroundAutoTiles.Sheets"/>) remakes the ground tiles and the ground tile
    ///   palettes, as setup's Make Ground Auto Tiles and Make Ground Tile Palettes steps do, and stamps that sheet so Check Art
    ///   Rules knows setup has caught up with it. Hand edited tiles are kept, as setup keeps them.
    /// - A moment after a Banks layer is painted, the console names every place near the new tiles where a bank takes a
    ///   shape no face piece draws (<see cref="OddShapes"/>), so it can be fixed while the brush is still in hand.
    ///   Bellwort Burrow > Tiles > List Odd Bank Shapes lists them all, and Straighten Banks To 45 Degrees redraws the steep
    ///   ones (<see cref="BankTools.StraightenBanks"/>). Banks are flat or 45 degrees, never steeper (Jocelyn, Oct 29).
    /// - The heights already work themselves out after a Banks or Ramps layer is painted (<see cref="TerrainHeightsBaker"/>).
    /// Bellwort Burrow > Tiles > Terrain Upkeep turns the first two on and off (on by default, remembered per machine).
    /// </summary>
    [InitializeOnLoad]
    public static class TerrainUpkeep
    {
        const string MenuPath = "Bellwort Burrow/Tiles/Terrain Upkeep";
        const string PrefKey = "BellwortBurrow.TerrainUpkeep";
        const double Settle = 1.0;   // seconds after the last painted tile

        static readonly Dictionary<Tilemap, List<Vector3Int>> painted = new();
        static readonly HashSet<string> savedSheets = new();
        static double lastChange;

        public static bool On
        {
            get => EditorPrefs.GetBool(PrefKey, true);
            set => EditorPrefs.SetBool(PrefKey, value);
        }

        static TerrainUpkeep()
        {
            Tilemap.tilemapTileChanged += OnTilesChanged;
            EditorApplication.update += OnUpdate;
        }

        [MenuItem(MenuPath, false, 400)]
        static void Toggle()
        {
            On = !On;
            Debug.Log($"Bellwort Burrow: terrain upkeep is {(On ? "on" : "off")}.");
        }

        [MenuItem(MenuPath, true)]
        static bool ToggleCheck()
        {
            Menu.SetChecked(MenuPath, On);
            return true;
        }

        // ---------- Ground sheets ----------

        /// <summary>Whether an imported asset is one of the ground sheets the ground tiles are made from.</summary>
        public static bool IsGroundSheet(string path) =>
            path.Replace('\\', '/').StartsWith(ArtLibrary.ArtRoot + "/") && path.EndsWith(".aseprite") &&
            GroundAutoTiles.Sheets.Any(s => s.Sheet == Path.GetFileNameWithoutExtension(path));

        internal static void SheetsImported(IEnumerable<string> paths)
        {
            if (!On || Application.isBatchMode) return;
            foreach (var path in paths)
                if (IsGroundSheet(path)) savedSheets.Add(path.Replace('\\', '/'));
        }

        static void RemakeGround()
        {
            var sheets = savedSheets.ToList();
            savedSheets.Clear();
            int tiles = GroundAutoTiles.MakeAll();
            var palettes = GroundPalette.MakeAll();
            if (palettes != null) SetupStamp.Restamp(sheets);
            Debug.Log($"Bellwort Burrow: {string.Join(", ", sheets.Select(Path.GetFileNameWithoutExtension))} saved, so the ground " +
                      $"tiles ({tiles} remade) and tile palettes were brought up to date (terrain upkeep).");
        }

        // ---------- Banks ----------

        static void OnTilesChanged(Tilemap map, Tilemap.SyncTile[] tiles)
        {
            if (!On || Application.isPlaying || map == null || map.name != "Banks") return;
            if (!painted.TryGetValue(map, out var cells)) painted[map] = cells = new List<Vector3Int>();
            foreach (var t in tiles) cells.Add(t.position);
            lastChange = EditorApplication.timeSinceStartup;
        }

        static void OnUpdate()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (savedSheets.Count > 0) RemakeGround();
            if (painted.Count == 0 || EditorApplication.timeSinceStartup - lastChange < Settle) return;
            var maps = painted.Where(m => m.Key != null).ToList();
            painted.Clear();
            foreach (var m in maps) Report(m.Key, m.Value);
        }

        // Only the odd shapes near what was just painted, so a bank left odd on purpose elsewhere doesn't come up every time.
        static void Report(Tilemap map, List<Vector3Int> cells)
        {
            bool Near(Vector3Int top) => cells.Any(c => Mathf.Abs(c.x - top.x) <= 2 && c.y <= top.y + 2 && c.y >= top.y - FaceTile.Rows - 4);
            var odd = OddShapes(map).Where(o => Near(o.cell)).ToList();
            if (odd.Count == 0) return;
            string report = string.Join("\n", odd.Select(o => $"  ({o.cell.x}, {o.cell.y}): {o.why}"));
            Debug.LogWarning($"Bellwort Burrow: the bank just painted on {map.gameObject.scene.name}'s {map.name} takes a shape no face " +
                             $"piece draws, so it shows a notch or a seam ({odd.Count} place(s), by the column's top cell):\n{report}", map);
        }

        /// <summary>Every odd bank shape in the open scenes' Banks layers, in the console: Bellwort Burrow > Tiles > List Odd Bank Shapes.</summary>
        [MenuItem("Bellwort Burrow/Tiles/List Odd Bank Shapes", false, 401)]
        static void ListAll()
        {
            int total = 0;
            foreach (var map in Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude).Where(m => m.name == "Banks"))
            {
                var odd = OddShapes(map);
                total += odd.Count;
                if (odd.Count > 0)
                    Debug.Log($"Bellwort Burrow: {odd.Count} odd bank shape(s) on {map.gameObject.scene.name}'s {map.name}:\n" +
                              string.Join("\n", odd.Select(o => $"  ({o.cell.x}, {o.cell.y}): {o.why}")), map);
            }
            if (total == 0) Debug.Log("Bellwort Burrow: every bank in the open scenes draws cleanly.");
        }

        /// <summary>
        /// Every place on a face tilemap whose shape the face pieces can't draw, by its column's top cell:
        /// - a column painted taller than a 45 degree run (more than <see cref="FaceTile.rows"/> + 1 rows);
        /// - a 45 degree column whose neighbour starts more than one row higher or lower, where the run would carry on
        ///   down or up a row (a sheer drop the 45 degree pieces don't have);
        /// - a column whose neighbour starts lower and ends higher than it does, so its foot and lip both stop inside the
        ///   neighbour's face.
        /// A flat run beside a flat run that starts any number of rows higher or lower is a step and is drawn, so it isn't listed.
        /// </summary>
        public static List<(Vector3Int cell, string why)> OddShapes(Tilemap map)
        {
            var found = new List<(Vector3Int, string)>();
            var b = map.cellBounds;
            TileBase At(Vector3Int p) => map.GetTile(p);
            for (int x = b.xMin; x < b.xMax; x++)
                for (int y = b.yMax - 1; y >= b.yMin; y--)
                {
                    if (!(At(new Vector3Int(x, y, 0)) is FaceTile tile)) continue;
                    var run = tile.RunAt(At, x, y).Value;
                    y = run.bottom;
                    var top = new Vector3Int(x, run.top, 0);
                    if (run.Height > tile.rows + 1)
                    {
                        found.Add((top, $"a column {run.Height} rows tall, where a face is {tile.rows} rows and a 45 degree run {tile.rows + 1}"));
                        continue;
                    }
                    foreach (int dx in new[] { -1, 1 })
                    {
                        if (!(tile.NeighbourRun(At, x + dx, run) is FaceTile.Run beside)) continue;
                        string side = dx < 0 ? "west" : "east";
                        if (run.Diagonal && Mathf.Abs(beside.top - run.top) > 1 && beside.Height <= tile.rows + 1)
                            found.Add((top, $"a 45 degree run whose {side} neighbour starts {Mathf.Abs(beside.top - run.top)} rows " +
                                            $"{(beside.top > run.top ? "higher" : "lower")}: steeper than 45 degrees, which banks never are (Tiles > Straighten Banks To 45 Degrees)"));
                        else if (beside.top > run.top && beside.bottom < run.bottom)
                            found.Add((top, $"its {side} neighbour is taller at both ends, so this column's lip and foot stop inside that face"));
                    }
                }
            return found;
        }
    }

    /// <summary>Hands the ground sheets Unity just imported to <see cref="TerrainUpkeep"/>.</summary>
    class TerrainUpkeepImports : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom) =>
            TerrainUpkeep.SheetsImported(imported.Concat(moved));
    }
}
