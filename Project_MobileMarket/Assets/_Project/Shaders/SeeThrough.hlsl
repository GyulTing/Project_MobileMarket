#ifndef MOBILEMARKET_SEE_THROUGH_INCLUDED
#define MOBILEMARKET_SEE_THROUGH_INCLUDED

// Set by WallSeeThrough.cs: xyz = the point to keep visible (player's chest), w = 1 while active.
float4 _SeeThroughTarget;
// x = radius (m) of the see-through hole around the camera->player line, y = soft edge width (m).
float4 _SeeThroughParams;

// Dithered hole in walls standing between the camera and the player. Shadows are untouched.
void SeeThroughClip(float4 positionCS)
{
    if (_SeeThroughTarget.w < 0.5) return;

    float2 uv = GetNormalizedScreenSpaceUV(positionCS);
    float3 positionWS = ComputeWorldSpacePosition(uv, positionCS.z, UNITY_MATRIX_I_VP);

    float3 toTarget = _SeeThroughTarget.xyz - _WorldSpaceCameraPos;
    float targetDist = length(toTarget);
    float3 dir = toTarget / targetDist;
    float3 toFrag = positionWS - _WorldSpaceCameraPos;
    float along = dot(toFrag, dir);
    if (along > targetDist - 0.4) return; // only what is in front of the player

    float offAxis = length(toFrag - dir * along);
    float fade = saturate((offAxis - _SeeThroughParams.x) / max(_SeeThroughParams.y, 0.001));

    // 4x4 Bayer dither so the hole edge breaks up instead of a hard circle.
    uint2 p = uint2(positionCS.xy) % 4;
    const float bayer[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
    float threshold = (bayer[p.y * 4 + p.x] + 0.5) / 16.0;
    clip(fade - threshold);
}

#endif
