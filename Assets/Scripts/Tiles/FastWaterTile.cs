using UnityEngine;
using UnityEngine.Tilemaps;

namespace BellwortBurrow.Tiles
{
    /// <summary>
    /// Fast water (art-4): painted on the Water layer below a waterfall and in the creek's narrow spots, where it joins
    /// the calm water as one creek (<see cref="WaterTile"/>). Each cell shows the calm water's piece for its layout,
    /// whose shape it also collides by, and animates over it streaks of current that grow, run down and fade inside
    /// the tile. Which streaks a cell gets (<see cref="LookAt"/>) is picked from where it is, one look in
    /// <see cref="lookWeights"/> being calm, so the streaks scatter instead of repeating tile after tile, and a streak
    /// never crosses into the next tile, so neighbours never have to line up.
    /// Setup makes the frames from the sheet: the calm water's pieces with their glints taken off and the
    /// fast_water_streaks slice laid over their open water (GroundAutoTiles, FastWaterFrames).
    /// </summary>
    [CreateAssetMenu(menuName = "Bellwort Burrow/Fast Water Tile")]
    public class FastWaterTile : WaterTile
    {
        [Tooltip("Frames per look.")]
        public int framesPerLook = 8;

        [Tooltip("How often each look comes up; the last is calm (one frame, no streaks).")]
        public int[] lookWeights = { 2, 2, 2, 2, 3 };

        [Tooltip("Frames a second (the Tilemap's Animation Frame Rate multiplies it).")]
        public float framesPerSecond = 1000f / 140f;

        [Tooltip("Every rule's frames, rule by rule in the order of the tiling rules: each streaked look's frames, then the calm frame.")]
        public Sprite[] frames = new Sprite[0];

        /// <summary>How many frames each rule has: every streaked look's, and the calm one.</summary>
        public int Stride => (lookWeights.Length - 1) * framesPerLook + 1;

        /// <summary>The look a cell shows, from where it is: an index into <see cref="lookWeights"/>.</summary>
        public static int LookAt(Vector3Int position, int[] weights)
        {
            int total = 0;
            foreach (var w in weights) total += w;
            if (total <= 0) return weights.Length - 1;
            unchecked
            {
                uint h = 2166136261u;
                h = (h ^ (uint)position.x) * 16777619u;
                h = (h ^ (uint)position.y) * 16777619u;
                h = (h ^ 5u) * 16777619u;
                h ^= h >> 13; h *= 0x5bd1e995u; h ^= h >> 15;
                int r = (int)(h % (uint)total);
                for (int i = 0; i < weights.Length; i++)
                {
                    if (r < weights[i]) return i;
                    r -= weights[i];
                }
            }
            return weights.Length - 1;
        }

        /// <summary>The index of the tiling rule a cell matches, or -1.</summary>
        int RuleAt(Vector3Int position, ITilemap tilemap)
        {
            var transform = Matrix4x4.identity;
            for (int i = 0; i < m_TilingRules.Count; i++)
                if (RuleMatches(m_TilingRules[i], position, tilemap, ref transform)) return i;
            return -1;
        }

        /// <summary>A cell's frames: its rule's frames for the look it picks (the calm look is a single frame).</summary>
        public Sprite[] FramesFor(int rule, int look)
        {
            int stride = Stride, calm = lookWeights.Length - 1;
            if (rule < 0 || (rule + 1) * stride > frames.Length) return null;
            int start = rule * stride + (look >= calm ? calm * framesPerLook : look * framesPerLook);
            var result = new Sprite[look >= calm ? 1 : framesPerLook];
            System.Array.Copy(frames, start, result, 0, result.Length);
            return result;
        }

        public override bool GetTileAnimationData(Vector3Int position, ITilemap tilemap, ref TileAnimationData tileAnimationData)
        {
            var sprites = FramesFor(RuleAt(position, tilemap), LookAt(position, lookWeights));
            if (sprites == null) return false;
            tileAnimationData.animatedSprites = sprites;
            tileAnimationData.animationSpeed = framesPerSecond;
            tileAnimationData.animationStartTime = 0f;
            return true;
        }
    }
}
