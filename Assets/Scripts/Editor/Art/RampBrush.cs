using System.Collections.Generic;
using System.Linq;
using BellwortBurrow.Tiles;
using UnityEditor;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace BellwortBurrow.EditorTools
{
    /// <summary>
    /// The Ramp brush in the Tile Palette (pick "Ramp Brush" in the brush menu under the palette). Choose earth or stone
    /// and North, East, West or Steps, then click a bank face on the Banks Tilemap: the whole ramp (or the steps) goes in,
    /// its bottom row on the face's foot, centred on the column you clicked. A North ramp and the steps take the width
    /// you set, and a North ramp the length, counted from the back of the cut to the face's foot (a longer ramp is
    /// gentler). Erasing a ramp or steps with this brush takes the whole block out and puts the face back.
    /// </summary>
    [CustomGridBrush(false, false, false, "Ramp Brush")]
    public class RampBrush : GridBrushBase
    {
        public enum Material { Earth, Stone }
        public enum Kind { North, East, West, Steps }

        public Material material = Material.Earth;
        public Kind kind = Kind.North;
        [Tooltip("North ramp and steps: how many columns wide. The walkway is two columns narrower plus two halves: 4 gives a walkway 3 tiles wide.")]
        [Min(2)] public int width = 4;
        [Tooltip("North ramp: how many rows tall, the face's 3 included. 7 is as drawn; longer is gentler.")]
        [Min(5)] public int length = 7;

        /// <summary>The tile to paint: the ground sheet's ramp or steps of this material and kind.</summary>
        public TileBase Tile()
        {
            string name = kind == Kind.Steps ? (material == Material.Earth ? "Log Steps" : "Stone Steps") : $"{material} Ramp {kind}";
            foreach (var ground in GroundAutoTiles.Sheets.Where(g => g.Banks))
            {
                var tile = AssetDatabase.LoadAssetAtPath<TileBase>(ground.Tile(name));
                if (tile != null) return tile;
            }
            return null;
        }

        /// <summary>
        /// The run of face this click is on: the face cells in that column within a few rows of the click, as (top,
        /// bottom). Null when there's no face near.
        /// </summary>
        public static (int top, int bottom)? FaceRunNear(Tilemap map, Vector3Int at, int reach = 5)
        {
            for (int d = 0; d <= reach; d++)
                foreach (int y in new[] { at.y - d, at.y + d })
                    if (map.GetTile(new Vector3Int(at.x, y, 0)) is FaceTile)
                    {
                        int top = y, bottom = y;
                        while (map.GetTile(new Vector3Int(at.x, top + 1, 0)) is FaceTile) top++;
                        while (map.GetTile(new Vector3Int(at.x, bottom - 1, 0)) is FaceTile) bottom--;
                        return (top, bottom);
                    }
            return null;
        }

        /// <summary>Where the block goes for a click at a column on a run of face: its left column, top row, size.</summary>
        public static RectInt BlockFor(Kind kind, TileBase tile, int width, int length, int column, (int top, int bottom) run)
        {
            int columns, rows;
            if (kind == Kind.Steps) { columns = width; rows = run.top - run.bottom + 1; }
            else if (tile is RampTile ramp && !ramp.stretches) { columns = ramp.columns; rows = ramp.rows; }
            else { columns = width; rows = length; }
            int left = column - (columns - 1) / 2;
            int top = kind == Kind.Steps ? run.top : run.bottom + rows - 1;
            return new RectInt(left, top, columns, rows);   // y is the top row; rows go down from it
        }

        public override void Paint(GridLayout grid, GameObject target, Vector3Int position)
        {
            var map = target != null ? target.GetComponent<Tilemap>() : null;
            if (map == null) return;
            var tile = Tile();
            if (tile == null) { Debug.LogWarning($"Bellwort Burrow: no {material} {kind} tile yet; run Set Up Forest Art."); return; }
            var run = FaceRunNear(map, position);
            if (run == null) { Debug.LogWarning("Bellwort Burrow: the Ramp brush goes on a bank face; click on one."); return; }
            var block = BlockFor(kind, tile, width, length, position.x, run.Value);
            Undo.RegisterCompleteObjectUndo(map, $"Paint {material} {kind}");
            for (int y = block.y; y > block.y - block.height; y--)
                for (int x = block.x; x < block.x + block.width; x++)
                    map.SetTile(new Vector3Int(x, y, 0), tile);
        }

        public override void Erase(GridLayout grid, GameObject target, Vector3Int position)
        {
            var map = target != null ? target.GetComponent<Tilemap>() : null;
            if (map == null) return;
            var tile = map.GetTile(position);
            if (!(tile is RampTile || tile is StepsTile)) return;
            Undo.RegisterCompleteObjectUndo(map, "Erase ramp");
            foreach (var (cell, face) in Restore(map, position)) map.SetTile(cell, face);
        }

        /// <summary>
        /// The cells of the ramp or steps block through a cell, each with what goes back there: the face beside the block
        /// in its rows (the bottom 3), nothing above them.
        /// </summary>
        public static List<(Vector3Int cell, TileBase face)> Restore(Tilemap map, Vector3Int at)
        {
            var tile = map.GetTile(at);
            var block = new HashSet<Vector3Int> { at };
            var todo = new Stack<Vector3Int>();
            todo.Push(at);
            while (todo.Count > 0 && block.Count < 4096)
            {
                var p = todo.Pop();
                foreach (var d in new[] { Vector3Int.left, Vector3Int.right, Vector3Int.up, Vector3Int.down })
                    if (map.GetTile(p + d) == tile && block.Add(p + d)) todo.Push(p + d);
            }
            int minX = block.Min(c => c.x), maxX = block.Max(c => c.x), bottom = block.Min(c => c.y);
            TileBase FaceBeside(int y) =>
                (map.GetTile(new Vector3Int(minX - 1, y, 0)) as FaceTile) ?? (TileBase)(map.GetTile(new Vector3Int(maxX + 1, y, 0)) as FaceTile)
                ?? map.GetTile(new Vector3Int(minX - 1, bottom, 0)) as FaceTile ?? map.GetTile(new Vector3Int(maxX + 1, bottom, 0)) as FaceTile;
            return block.Select(c => (c, c.y < bottom + FaceTile.Rows ? FaceBeside(c.y) : null)).ToList();
        }
    }

    /// <summary>The Ramp brush's settings in the Tile Palette.</summary>
    [CustomEditor(typeof(RampBrush))]
    public class RampBrushEditor : GridBrushEditorBase
    {
        public override void OnPaintInspectorGUI()
        {
            var brush = (RampBrush)target;
            EditorGUI.BeginChangeCheck();
            var material = (RampBrush.Material)EditorGUILayout.EnumPopup("Material", brush.material);
            var kind = (RampBrush.Kind)EditorGUILayout.EnumPopup("Kind", brush.kind);
            int width = brush.width, length = brush.length;
            if (kind == RampBrush.Kind.North || kind == RampBrush.Kind.Steps)
                width = Mathf.Max(2, EditorGUILayout.IntField("Width (columns)", brush.width));
            if (kind == RampBrush.Kind.North)
                length = Mathf.Max(5, EditorGUILayout.IntField("Length (rows)", brush.length));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(brush, "Ramp Brush settings");
                brush.material = material; brush.kind = kind; brush.width = width; brush.length = length;
                EditorUtility.SetDirty(brush);
            }
            EditorGUILayout.HelpBox(kind == RampBrush.Kind.North ? "Click a bank face: the ramp's bottom row goes on the face's foot. A walkway 3 tiles wide is 4 columns."
                : kind == RampBrush.Kind.Steps ? "Click a bank face: the steps take the face's rows. 3 columns are 3 tiles of steps."
                : "Click a bank face: the ramp takes 6 columns of it, its bottom row on the face's foot, and climbs " + (kind == RampBrush.Kind.East ? "east." : "west."), MessageType.None);
        }
    }
}
