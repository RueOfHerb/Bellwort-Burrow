using System;
using UnityEngine;

namespace BellwortBurrow.Systems.World
{
    /// <summary>
    /// How a shadow stretches and swings as its light crosses the sky, for the moon and the sun alike. The light rises
    /// in the east and sets in the west; u runs from 0 at rise to 1 at set.
    /// - Scale: cot(e(u)), with the light's elevation e(u) = eMin + (ePeak - eMin) * sin(pi u). Longest at rise and
    ///   set, when the light is low, shortest halfway, when it's highest. eMin stays above zero so the shadow never
    ///   runs off to infinity at the horizon.
    /// - Angle: thetaMax * cos(pi u). Pivoted clockwise by thetaMax at rise, straight halfway, counterclockwise by
    ///   thetaMax at set. It swings fastest around the middle of the run and slowest near the horizons.
    /// (Jocelyn, Oct 21, 2026.)
    /// </summary>
    public static class ShadowCurve
    {
        /// <summary>The light's elevation above the horizon, in degrees, at u (0 at rise, 1 at set).</summary>
        public static float ElevationAt(float u, float eMinDegrees, float ePeakDegrees)
        {
            u = Mathf.Clamp01(u);
            return eMinDegrees + (ePeakDegrees - eMinDegrees) * Mathf.Sin(Mathf.PI * u);
        }

        /// <summary>How far the shadow stretches at u: cot of the light's elevation, so 1 when the light is at 45 degrees.</summary>
        public static float ScaleAt(float u, float eMinDegrees, float ePeakDegrees)
        {
            float e = Mathf.Max(0.5f, ElevationAt(u, eMinDegrees, ePeakDegrees)) * Mathf.Deg2Rad;
            return 1f / Mathf.Tan(e);
        }

        /// <summary>The shadow's pivot at u, in degrees: positive is clockwise.</summary>
        public static float AngleAt(float u, float thetaMaxDegrees)
        {
            return thetaMaxDegrees * Mathf.Cos(Mathf.PI * Mathf.Clamp01(u));
        }

        /// <summary>Where a light is in its run: 0 at rise, 1 at set, for an hour of the day (wraps past midnight).</summary>
        public static float Travel(float hour, float rises, float sets)
        {
            float span = (sets - rises + 24f) % 24f;
            if (span <= 0f) return 0f;
            float since = (hour - rises + 24f) % 24f;
            return Mathf.Clamp01(since / span);
        }
    }
}
