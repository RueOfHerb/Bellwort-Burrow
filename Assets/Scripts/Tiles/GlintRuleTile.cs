using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace BellwortBurrow.Tiles
{
    /// <summary>
    /// A ground set whose pieces carry glints: a Rule Tile like the paths', where a piece with Glow pixels (the
    /// tillable dirt's specks) also gets a small unlit picture of them on top, so they glint under the moonlight while
    /// the dirt around them dims. Setup makes these from the sheet (GroundAutoTiles) and fills in the pairs; a piece
    /// without glints gets nothing extra.
    /// The glint picture is the rule's GameObject (<see cref="glintObject"/>, a SpriteRenderer on the unlit material),
    /// placed on the cell and drawn one step above the Tilemap it is painted on, in the same sorting layer. Anything laid
    /// over this ground on another Tilemap (farmland) needs an Order in Layer at least two above it to cover the glints.
    /// </summary>
    [CreateAssetMenu(menuName = "Bellwort Burrow/Glint Rule Tile")]
    public class GlintRuleTile : RuleTile
    {
        [Tooltip("The pieces that have glints, each paired with its glint picture at the same index in Glints. Setup fills these in.")]
        public List<Sprite> glintPieces = new List<Sprite>();

        [Tooltip("The glint picture of each piece in Glint Pieces.")]
        public List<Sprite> glints = new List<Sprite>();

        [Tooltip("What a glinting cell gets on top: a SpriteRenderer on the unlit material. Setup makes it.")]
        public GameObject glintObject;

        /// <summary>A piece's glint picture, or null when it has none.</summary>
        public Sprite GlintFor(Sprite piece)
        {
            if (piece == null) return null;
            int i = glintPieces.IndexOf(piece);
            return i >= 0 && i < glints.Count ? glints[i] : null;
        }

        public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
        {
            base.GetTileData(position, tilemap, ref tileData);
            tileData.gameObject = GlintFor(tileData.sprite) != null ? glintObject : null;
        }

        public override bool StartUp(Vector3Int position, ITilemap tilemap, GameObject instantiatedGameObject)
        {
            bool started = base.StartUp(position, tilemap, instantiatedGameObject);
            if (instantiatedGameObject == null) return started;
            var picture = instantiatedGameObject.GetComponent<SpriteRenderer>();
            if (picture == null) return started;
            picture.sprite = GlintFor(tilemap.GetSprite(position));
            var under = tilemap.GetComponent<TilemapRenderer>();
            if (under != null)
            {
                picture.sortingLayerID = under.sortingLayerID;
                picture.sortingOrder = under.sortingOrder + 1;
            }
            return started;
        }
    }
}
