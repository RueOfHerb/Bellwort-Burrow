using UnityEngine;
using UnityEngine.Tilemaps;

namespace BellwortBurrow.Tiles
{
    /// <summary>
    /// The creek's water (Burrow Water, Faerie Field Water): a Rule Tile like the other ground sets, except that it
    /// counts the tiles in <see cref="sameWater"/> as itself when it reads its neighbours, so calm water and Fast Water
    /// (<see cref="FastWaterTile"/>) painted side by side on the Water layer run on as one creek, with no bank drawn
    /// where they meet (art-4). Setup fills <see cref="sameWater"/> in.
    /// </summary>
    [CreateAssetMenu(menuName = "Bellwort Burrow/Water Tile")]
    public class WaterTile : RuleTile
    {
        [Tooltip("Other water tiles that join this one as if they were the same tile (calm water and Fast Water).")]
        public TileBase[] sameWater = new TileBase[0];

        /// <summary>Whether a neighbouring tile is this water or water that joins it.</summary>
        public bool IsSameWater(TileBase other)
        {
            if (other == this) return true;
            if (other == null) return false;
            foreach (var water in sameWater) if (water == other) return true;
            return false;
        }

        public override bool RuleMatch(int neighbor, TileBase other)
        {
            if (other is RuleOverrideTile overriding) other = overriding.m_InstanceTile;
            switch (neighbor)
            {
                case TilingRuleOutput.Neighbor.This: return IsSameWater(other);
                case TilingRuleOutput.Neighbor.NotThis: return !IsSameWater(other);
            }
            return base.RuleMatch(neighbor, other);
        }
    }
}
