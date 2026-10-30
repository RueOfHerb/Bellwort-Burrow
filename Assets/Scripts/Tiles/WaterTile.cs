using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace BellwortBurrow.Tiles
{
    /// <summary>
    /// The creek's water (Burrow Water, Faerie Field Water): a Rule Tile like the other ground sets, except that it
    /// counts the tiles in <see cref="sameWater"/> as itself when it reads its neighbours, so calm water and Fast Water
    /// (<see cref="FastWaterTile"/>) painted side by side on the Water layer run on as one creek, with no bank drawn
    /// where they meet (art-4). Setup fills <see cref="sameWater"/> in.
    /// The calm water idles too (Jocelyn, Oct 30): each cell keeps the piece the Rule Tile picks for it, glints and
    /// all, and now and then a small squiggle grows on its open water, drifts a pixel and fades. Which squiggles a
    /// cell gets is picked from where it is (<see cref="idleLookWeights"/>, the last look still); setup makes every
    /// piece's frames from the sheet's idle_water_squiggles slice (FastWaterFrames.MakeIdle).
    /// </summary>
    [CreateAssetMenu(menuName = "Bellwort Burrow/Water Tile")]
    public class WaterTile : RuleTile
    {
        [Tooltip("Other water tiles that join this one as if they were the same tile (calm water and Fast Water).")]
        public TileBase[] sameWater = new TileBase[0];

        [Tooltip("The pieces that idle: each one's frames sit in Idle Frames, in this order.")]
        public Sprite[] idlePieces = new Sprite[0];

        [Tooltip("Every idle piece's frames, piece by piece: each look's frames in turn (a frame with no squiggle is the piece itself).")]
        public Sprite[] idleFrames = new Sprite[0];

        [Tooltip("Frames per idle look.")]
        public int idleFramesPerLook = 16;

        [Tooltip("How often each idle look comes up; the last is still.")]
        public int[] idleLookWeights = new int[0];

        [Tooltip("Idle frames a second (the Tilemap's Animation Frame Rate multiplies it).")]
        public float idleFramesPerSecond = 5f;

        /// <summary>The salt that keeps the idle looks' pick apart from the fast water's.</summary>
        public const uint IdleSalt = 43;

        [System.NonSerialized] Dictionary<Sprite, int> idleIndex;

        void OnValidate() => idleIndex = null;

        /// <summary>Where a piece's idle frames start, or -1 when it has none.</summary>
        int IdleStart(Sprite piece)
        {
            if (piece == null) return -1;
            if (idleIndex == null || idleIndex.Count != idlePieces.Length)
            {
                idleIndex = new Dictionary<Sprite, int>();
                for (int i = 0; i < idlePieces.Length; i++) if (idlePieces[i] != null) idleIndex[idlePieces[i]] = i;
            }
            return idleIndex.TryGetValue(piece, out int at) ? at * (idleLookWeights.Length - 1) * idleFramesPerLook : -1;
        }

        /// <summary>A cell's idle frames for this piece and look, or null when it stays still.</summary>
        public Sprite[] IdleFramesFor(Sprite piece, int look)
        {
            int looks = idleLookWeights.Length - 1;
            if (looks <= 0 || look >= looks || look < 0) return null;
            int start = IdleStart(piece);
            if (start < 0) return null;
            start += look * idleFramesPerLook;
            if (start + idleFramesPerLook > idleFrames.Length) return null;
            var result = new Sprite[idleFramesPerLook];
            System.Array.Copy(idleFrames, start, result, 0, idleFramesPerLook);
            return result;
        }

        public override bool GetTileAnimationData(Vector3Int position, ITilemap tilemap, ref TileAnimationData tileAnimationData)
        {
            if (idleFrames.Length == 0 || idleLookWeights.Length < 2) return false;
            var data = new TileData();
            GetTileData(position, tilemap, ref data);
            var frames = IdleFramesFor(data.sprite, FastWaterTile.LookAt(position, idleLookWeights, IdleSalt));
            if (frames == null) return false;
            tileAnimationData.animatedSprites = frames;
            tileAnimationData.animationSpeed = idleFramesPerSecond;
            tileAnimationData.animationStartTime = 0f;
            return true;
        }

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
