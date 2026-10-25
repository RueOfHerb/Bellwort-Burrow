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
