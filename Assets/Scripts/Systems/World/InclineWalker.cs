using System.Collections.Generic;
using System.Linq;
using BellwortBurrow.Tiles;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace BellwortBurrow.Systems.World
{
    /// <summary>
    /// Makes whoever carries it slower walking uphill and quicker walking downhill. It looks under its feet for an
    /// incline: a bridge's arch first (<see cref="IIncline"/>), then any slope, ramp or step tile (<see cref="IInclineTile"/>)
    /// on the scene's Tilemaps. Walking straight up the steepest kind moves at <see cref="uphillSpeed"/> of the walking
    /// speed and straight down it at <see cref="downhillSpeed"/>; walking across a slope is unchanged, a diagonal is in
    /// between, and a gentler slope has less effect. The PlayerController adds one if the player has none, with these defaults.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InclineWalker : MonoBehaviour
    {
        [Tooltip("Speed walking straight up the steepest slope or steps, as a share of walking speed.")]
        [Range(0.25f, 1f)] public float uphillSpeed = 0.85f;
        [Tooltip("Speed walking straight down the steepest slope or steps, as a share of walking speed.")]
        [Range(1f, 2f)] public float downhillSpeed = 1.10f;
        [Tooltip("Where the feet are, from the body's position, in units.")]
        public Vector2 feet = Vector2.zero;

        static readonly List<IIncline> Sources = new List<IIncline>();

        List<Tilemap> maps;
        Rigidbody2D body;
        Collider2D walker;

        /// <summary>Adds a bridge or other incline that isn't a tile, while it's enabled.</summary>
        public static void Register(IIncline source)
        {
            if (source != null && !Sources.Contains(source)) Sources.Add(source);
        }

        public static void Unregister(IIncline source) => Sources.Remove(source);

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            walker = GetComponents<Collider2D>().FirstOrDefault(c => !c.isTrigger);
        }

        /// <summary>Looks for slope tiles on these Tilemaps instead of finding them in the scene.</summary>
        public void UseMaps(IEnumerable<Tilemap> tilemaps) => maps = Sort(tilemaps);

        /// <summary>Finds the scene's Tilemaps that have slope, ramp or step tiles painted on them.</summary>
        public void FindMaps() => maps = Sort(FindObjectsByType<Tilemap>());

        static List<Tilemap> Sort(IEnumerable<Tilemap> tilemaps)
        {
            return tilemaps
                .Where(m => m != null && HasInclines(m))
                .OrderByDescending(m => m.TryGetComponent<TilemapRenderer>(out var r) ? r.sortingOrder : 0)
                .ToList();
        }

        static bool HasInclines(Tilemap map)
        {
            var tiles = new TileBase[map.GetUsedTilesCount()];
            map.GetUsedTilesNonAlloc(tiles);
            return tiles.Any(t => t is IInclineTile);
        }

        Vector2 Feet => (body != null ? body.position : (Vector2)transform.position) + feet;

        /// <summary>Which way is uphill under the feet, its length how steep (zero on level ground).</summary>
        public Vector2 UphillHere()
        {
            Vector2 at = Feet;
            if (walker == null) walker = GetComponents<Collider2D>().FirstOrDefault(c => !c.isTrigger);
            foreach (var source in Sources)
                if (source != null && source.TryGetUphill(walker, at, out var carried))
                    return carried;
            if (maps == null) FindMaps();
            foreach (var map in maps)
            {
                if (map == null || !map.isActiveAndEnabled) continue;
                var cell = map.WorldToCell(at);
                if (map.GetTile(cell) is IInclineTile slope)
                    return slope.Uphill(map, cell);
            }
            return Vector2.zero;
        }

        /// <summary>How fast to walk in this direction here, as a share of walking speed.</summary>
        public float SpeedFor(Vector2 move) => Factor(move, UphillHere(), uphillSpeed, downhillSpeed);

        /// <summary>
        /// The share of walking speed for a move on a slope: <paramref name="uphillSpeed"/> straight up the steepest
        /// slope (an uphill 1 long), <paramref name="downhillSpeed"/> straight down it, 1 across it or on level ground,
        /// and in between for diagonals and gentler slopes.
        /// </summary>
        public static float Factor(Vector2 move, Vector2 uphill, float uphillSpeed, float downhillSpeed)
        {
            if (move.sqrMagnitude < 1e-8f || uphill.sqrMagnitude < 1e-8f) return 1f;
            float steep = Mathf.Min(uphill.magnitude, 1f);
            float along = Vector2.Dot(move.normalized, uphill.normalized) * steep;
            return along >= 0f ? Mathf.Lerp(1f, uphillSpeed, along) : Mathf.Lerp(1f, downhillSpeed, -along);
        }
    }
}
