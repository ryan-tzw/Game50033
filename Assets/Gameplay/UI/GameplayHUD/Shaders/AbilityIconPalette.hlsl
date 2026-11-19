#ifndef CRESCENDO_ABILITY_ICON_PALETTE_INCLUDED
#define CRESCENDO_ABILITY_ICON_PALETTE_INCLUDED

void AbilityIconPalette_float(float4 Source, float4 Shadows, float4 DarkMidtones,
    float4 LightMidtones, float4 Highlights, out float3 Color, out float Alpha)
{
    float brightness = saturate(Source.r);
    float3 shadows = Shadows.rgb;
    float3 darkMidtones = DarkMidtones.rgb;
    float3 lightMidtones = LightMidtones.rgb;
    float3 highlights = Highlights.rgb;
#ifndef UNITY_COLORSPACE_GAMMA
    // interpolate in authored colour space so a neutral palette preserves the source greys.
    brightness = LinearToSRGB(brightness);
    shadows = LinearToSRGB(shadows);
    darkMidtones = LinearToSRGB(darkMidtones);
    lightMidtones = LinearToSRGB(lightMidtones);
    highlights = LinearToSRGB(highlights);
#endif
    float position = brightness * 3.0;
    Color = lerp(shadows, darkMidtones, saturate(position));
    Color = lerp(Color, lightMidtones, saturate(position - 1.0));
    Color = lerp(Color, highlights, saturate(position - 2.0));
#ifndef UNITY_COLORSPACE_GAMMA
    Color = SRGBToLinear(Color);
#endif
    // palette alpha does not alter the original artwork or UI Toolkit's opacity.
    Alpha = Source.a;
}

#endif
