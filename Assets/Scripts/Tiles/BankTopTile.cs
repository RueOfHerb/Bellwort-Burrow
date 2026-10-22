using UnityEngine;
using UnityEngine.Tilemaps;

namespace BellwortBurrow.Tiles
{
    /// <summary>
    /// Raised ground's top: a Rule Tile (its rules made by setup, see GroundAutoTiles) that also counts the top cell of a
    /// 45 degree face beside it as more of itself, since that cell's upper part is this same grass running on down the
    /// slope. So a top above a 45 degree run has no rim down its side, while a top beside a step keeps its rim.
    /// </summary>
    [CreateAssetMenu(menuName = "Bellwort Burrow/Bank Top Tile")]
    public class BankTopTile : RuleTile
    {
        /// <summary>Whether the cell at a position is this top, or reads as part of it.</summary>
        public bool Counts(ITilemap tilemap, Vector3Int position)
        {
            var other = tilemap.GetTile(position);
            if (other == this) return true;
            return other is FaceTile face && face.IsDiagonalLip(tilemap, position);
        }

        public override bool RuleMatches(TilingRule rule, Vector3Int position, ITilemap tilemap, ref Matrix4x4 transform)
        {
            transform = Matrix4x4.identity;
            for (int i = 0; i < rule.m_Neighbors.Count && i < rule.m_NeighborPositions.Count; i++)
            {
                bool painted = Counts(tilemap, position + rule.m_NeighborPositions[i]);
                switch (rule.m_Neighbors[i])
                {
                    case TilingRuleOutput.Neighbor.This when !painted: return false;
                    case TilingRuleOutput.Neighbor.NotThis when painted: return false;
                }
            }
            return true;
        }
    }
}
