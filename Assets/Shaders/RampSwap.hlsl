#ifndef BELLWORT_RAMP_SWAP_INCLUDED
#define BELLWORT_RAMP_SWAP_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

// Player recoloring (sys-6). Player Appearance sets these per renderer: up to 16 colors to find and the colors to
// show instead, both in sRGB, and how many pairs are in use. Colors are matched exactly (the player textures are
// uncompressed and point filtered, and no two colors in the characters palette are the same).
float4 _SwapFrom[16];
float4 _SwapTo[16];
float _SwapCount;

float3 BellwortRampSwap(float3 color)
{
    int count = (int)_SwapCount;
    if (count <= 0) return color;

#if defined(UNITY_COLORSPACE_GAMMA)
    float3 srgb = color;
#else
    float3 srgb = LinearToSRGB(color);
#endif

    for (int i = 0; i < 16; i++)
    {
        if (i >= count) break;
        float3 difference = abs(srgb - _SwapFrom[i].rgb);
        if (max(difference.r, max(difference.g, difference.b)) < 0.5 / 255.0)
        {
#if defined(UNITY_COLORSPACE_GAMMA)
            return _SwapTo[i].rgb;
#else
            return SRGBToLinear(_SwapTo[i].rgb);
#endif
        }
    }
    return color;
}

#endif
