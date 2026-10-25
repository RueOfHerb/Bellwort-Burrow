using UnityEngine;
using UnityEngine.Tilemaps;

namespace BellwortBurrow.Tiles
{
    /// <summary>
    /// What lies under a ramp or steps: the face on the other Tilemaps of the same grid. When a ramp or steps is painted
    /// or erased, the face under it is refreshed so it stops or starts blocking (<see cref="FaceTile.Covered"/>).
    /// </summary>
    public static class Under
    {
        public static void Refresh(ITilemap tilemap, Vector3Int position)
        {
            var map = tilemap.GetComponent<Tilemap>();
            if (map == null || map.transform.parent == null) return;
            foreach (Transform sibling in map.transform.parent)
                if (sibling != map.transform && sibling.TryGetComponent<Tilemap>(out var other) && other.GetTile(position) is FaceTile)
                    other.RefreshTile(position);
        }
    }
}
