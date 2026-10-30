using System.Collections.Generic;
using System.Linq;
using BellwortBurrow.Tiles;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace BellwortBurrow.EditorTools
{
    /// <summary>
    /// Tools for the raised ground's faces on a Tilemap: closing the steps a bank was painted in (sys-7.15).
    /// </summary>
    public static class BankTools
    {
        /// <summary>
        /// Closes every step in a bank: where two flat runs of face 3 rows tall sit side by side one row apart, the column
        /// on the low side grows one row up, so it turns 45 degrees and the bank carries on round the step instead of
        /// ending open. A lone 45 degree column is a jog, so the runs beside it get no crease (<see cref="FaceTile"/>).
        /// Returns the cells it added.
        /// </summary>
        public static List<Vector3Int> CloseSteps(Tilemap map)
        {
            var b = map.cellBounds;
            bool Face(int x, int y) => map.GetTile(new Vector3Int(x, y, 0)) is FaceTile;
            List<(int top, int bottom)> Runs(int x)
            {
                var runs = new List<(int, int)>();
                for (int y = b.yMax; y >= b.yMin; y--)
                    if (Face(x, y))
                    {
                        int top = y;
                        while (Face(x, y - 1)) y--;
                        runs.Add((top, y));
                    }
                return runs;
            }
            var adds = new List<(Vector3Int cell, TileBase tile)>();
            for (int x = b.xMin; x < b.xMax - 1; x++)
                foreach (var l in Runs(x))
                    foreach (var r in Runs(x + 1))
                    {
                        if (l.top - l.bottom != FaceTile.Rows - 1 || r.top - r.bottom != FaceTile.Rows - 1) continue;
                        if (l.top == r.top + 1 && l.bottom == r.bottom + 1)
                            adds.Add((new Vector3Int(x + 1, r.top + 1, 0), map.GetTile(new Vector3Int(x + 1, r.top, 0))));
                        if (r.top == l.top + 1 && r.bottom == l.bottom + 1)
                            adds.Add((new Vector3Int(x, l.top + 1, 0), map.GetTile(new Vector3Int(x, l.top, 0))));
                    }
            var added = new List<Vector3Int>();
            foreach (var (cell, tile) in adds)
                if (map.GetTile(cell) == null) { map.SetTile(cell, tile); added.Add(cell); }
            return added;
        }

        /// <summary>
        /// Redraws every bank that falls or climbs faster than 45 degrees as 45 degree runs (arc-11.2), keeping its high
        /// end where it is: along each run of joined columns, the lip's height at every column edge is raised to at least the
        /// lip anywhere along the run less one row a column, so it never moves more than a row a column. Each column is then
        /// painted again from its two edges: flat where they're level, a 45 degree column where they differ by a row. A column
        /// taller than a 45 degree run keeps its top rows. A step between two flat runs is left as a step, and so is a column
        /// <paramref name="keep"/> names (by default <see cref="KeptBySiblings"/>: under water, a waterfall or a ramp). The bank moves
        /// toward its low side where it was steep. Returns the cells painted and cleared.
        /// </summary>
        public static (List<Vector3Int> painted, List<Vector3Int> cleared) StraightenBanks(Tilemap map,
            System.Func<int, FaceTile.Run, bool> keep = null)
        {
            keep ??= KeptBySiblings(map);
            // only the runs with a shape no piece draws are redrawn; a column shorter than a face (cut off at the map's edge,
            // or painted short on purpose) stays as painted
            var odd = new HashSet<Vector3Int>(TerrainUpkeep.OddShapes(map).Select(o => o.cell));
            TileBase At(Vector3Int p) => map.GetTile(p);
            var b = map.cellBounds;
            // every column's runs, west to east
            var columns = new Dictionary<int, List<FaceTile.Run>>();
            for (int x = b.xMin; x < b.xMax; x++)
            {
                var runs = new List<FaceTile.Run>();
                for (int y = b.yMax - 1; y >= b.yMin; y--)
                    if (At(new Vector3Int(x, y, 0)) is FaceTile tile)
                    {
                        var run = tile.RunAt(At, x, y).Value;
                        runs.Add(run);
                        y = run.bottom;
                    }
                columns[x] = runs;
            }
            FaceTile TileOf(int x, FaceTile.Run r) => (FaceTile)At(new Vector3Int(x, r.top, 0));
            // a run's lip at its west and east edges, in rows
            (int west, int east) Lips(int x, FaceTile.Run r)
            {
                var tile = TileOf(x, r);
                if (r.Height != tile.rows + 1) return (r.top + 1, r.top + 1);
                return Shape(tile, At, x, r) == 'U' ? (r.top, r.top + 1) : (r.top + 1, r.top);
            }
            bool Same(FaceTile.Run p, FaceTile.Run q) => p.top == q.top && p.bottom == q.bottom;

            var painted = new List<Vector3Int>();
            var cleared = new List<Vector3Int>();
            var done = new HashSet<(int, int)>();
            for (int x0 = b.xMin; x0 < b.xMax; x0++)
                foreach (var start in columns[x0])
                {
                    if (done.Contains((x0, start.top))) continue;
                    // follow the run east while the columns join each other both ways and it isn't a step between flats
                    var chain = new List<(int x, FaceTile.Run run)> { (x0, start) };
                    done.Add((x0, start.top));
                    while (true)
                    {
                        var (x, run) = chain[chain.Count - 1];
                        var tile = TileOf(x, run);
                        if (!(tile.NeighbourRun(At, x + 1, run) is FaceTile.Run next)) break;
                        if (!(tile.NeighbourRun(At, x, next) is FaceTile.Run back) || !Same(back, run)) break;
                        var nextTile = TileOf(x + 1, next);
                        bool flatStep = run.Height <= tile.rows && next.Height <= nextTile.rows && next.top != run.top;
                        if (flatStep || done.Contains((x + 1, next.top))) break;
                        chain.Add((x + 1, next));
                        done.Add((x + 1, next.top));
                    }
                    // the lip at each of the chain's edges, the higher of the two where it jumps
                    int n = chain.Count;
                    var lip = new int[n + 1];
                    for (int i = 0; i < n; i++)
                    {
                        var (w, e) = Lips(chain[i].x, chain[i].run);
                        lip[i] = i == 0 ? w : Mathf.Max(lip[i], w);
                        lip[i + 1] = e;
                    }
                    var raised = (int[])lip.Clone();
                    for (int i = 1; i <= n; i++) raised[i] = Mathf.Max(raised[i], raised[i - 1] - 1);
                    for (int i = n - 1; i >= 0; i--) raised[i] = Mathf.Max(raised[i], raised[i + 1] - 1);
                    if (!chain.Any(c => odd.Contains(new Vector3Int(c.x, c.run.top, 0)))) continue;
                    var kept = chain.Select(c => c.run.Height < TileOf(c.x, c.run).rows || keep(c.x, c.run)).ToArray();
                    for (int i = 0; i < n; i++)
                    {
                        if (kept[i]) continue;   // water runs through it or a ramp lies over it: left as painted
                        var (x, run) = chain[i];
                        var tile = TileOf(x, run);
                        int w = raised[i], e = raised[i + 1];
                        int top = Mathf.Min(w, e) - (w == e ? 1 : 0), rows = w == e ? tile.rows : tile.rows + 1;
                        if (w == e) top = w - 1;
                        int bottom = top - rows + 1;
                        if (top == run.top && bottom == run.bottom) continue;
                        for (int y = run.bottom; y <= run.top; y++)
                            if (y < bottom || y > top) { var c = new Vector3Int(x, y, 0); map.SetTile(c, null); cleared.Add(c); }
                        for (int y = bottom; y <= top; y++)
                        {
                            var c = new Vector3Int(x, y, 0);
                            if (At(c) == null) { map.SetTile(c, tile); painted.Add(c); }
                        }
                    }
                }
            return (painted, cleared);
        }

        /// <summary>
        /// Columns of face that stay as painted when banks are straightened: where water or a waterfall runs through the face
        /// (so the waterfall keeps the flat run it drops off) or a ramp or steps lie over it, on the layers beside this one.
        /// </summary>
        public static System.Func<int, FaceTile.Run, bool> KeptBySiblings(Tilemap map)
        {
            var others = new List<Tilemap>();
            if (map.transform.parent != null)
                foreach (Transform sibling in map.transform.parent)
                    if (sibling != map.transform && sibling.TryGetComponent<Tilemap>(out var other) &&
                        (other.name == "Water" || other.name == "Waterfalls" || other.name == "Ramps"))
                        others.Add(other);
            return (x, run) =>
            {
                for (int dx = -1; dx <= 1; dx++)
                    for (int y = run.bottom - 1; y <= run.top + 1; y++)
                        foreach (var other in others)
                            if (other.GetTile(new Vector3Int(x + dx, y, 0)) != null && (dx == 0 || other.name != "Water"))
                                return true;
                return false;
            };
        }

        // F for a flat run, D for a 45 degree run down to the east, U up: the first letter of the face tile's own piece key.
        static char Shape(FaceTile tile, System.Func<Vector3Int, TileBase> at, int x, FaceTile.Run run)
        {
            if (!run.Diagonal) return 'F';
            var key = tile.KeyAt(at, new Vector3Int(x, run.top, 0)).key;
            return string.IsNullOrEmpty(key) ? 'D' : key[0];
        }

        [MenuItem("Bellwort Burrow/Tiles/Straighten Banks To 45 Degrees")]
        static void StraightenFromMenu()
        {
            var maps = Selection.gameObjects.Select(g => g.GetComponent<Tilemap>()).Where(m => m != null).ToList();
            if (maps.Count == 0)
                maps = Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude).Where(HasFaces).ToList();
            foreach (var map in maps)
            {
                Undo.RegisterCompleteObjectUndo(map, "Straighten Banks To 45 Degrees");
                var (painted, cleared) = StraightenBanks(map);
                if (painted.Count + cleared.Count > 0) EditorSceneManager.MarkSceneDirty(map.gameObject.scene);
                Debug.Log($"Bellwort Burrow: straightened the banks on {map.name}: {painted.Count} face cells painted, {cleared.Count} cleared.", map);
            }
        }

        [MenuItem("Bellwort Burrow/Tiles/Close Bank Steps")]
        static void CloseStepsFromMenu()
        {
            var maps = Selection.gameObjects.Select(g => g.GetComponent<Tilemap>()).Where(m => m != null).ToList();
            if (maps.Count == 0)
                maps = Object.FindObjectsByType<Tilemap>().Where(HasFaces).ToList();
            int total = 0;
            foreach (var map in maps)
            {
                Undo.RegisterCompleteObjectUndo(map, "Close Bank Steps");
                int n = CloseSteps(map).Count;
                total += n;
                if (n > 0) EditorSceneManager.MarkSceneDirty(map.gameObject.scene);
            }
            Debug.Log($"Bellwort Burrow: closed {total} bank steps on {string.Join(", ", maps.Select(m => m.name))}.");
        }

        static bool HasFaces(Tilemap map)
        {
            var tiles = new TileBase[map.GetUsedTilesCount()];
            map.GetUsedTilesNonAlloc(tiles);
            return tiles.Any(t => t is FaceTile);
        }
    }
}
