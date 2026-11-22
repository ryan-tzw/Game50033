#ifndef CRESCENDO_ABILITY_ICON_PALETTE_INCLUDED
#define CRESCENDO_ABILITY_ICON_PALETTE_INCLUDED

void AbilityIconPalette_float(float4 Source, float4 Shadows, float4 DarkMidtones,
    float4 LightMidtones, float4 Highlights,
    float4 BackgroundShadows, float4 BackgroundDarkMidtones, float4 BackgroundLightMidtones, float4 BackgroundHighlights,
    float4 BorderShadows, float4 BorderDarkMidtones, float4 BorderLightMidtones, float4 BorderHighlights,
    float PaletteLayer, out float3 Color, out float Alpha)
{
    float brightness = saturate(Source.r);
    // one class material supplies independent palettes for its three artwork layers.
    float3 shadows = PaletteLayer < 0.5 ? Shadows.rgb : PaletteLayer < 1.5 ? BackgroundShadows.rgb : BorderShadows.rgb;
    float3 darkMidtones = PaletteLayer < 0.5 ? DarkMidtones.rgb : PaletteLayer < 1.5 ? BackgroundDarkMidtones.rgb : BorderDarkMidtones.rgb;
    float3 lightMidtones = PaletteLayer < 0.5 ? LightMidtones.rgb : PaletteLayer < 1.5 ? BackgroundLightMidtones.rgb : BorderLightMidtones.rgb;
    float3 highlights = PaletteLayer < 0.5 ? Highlights.rgb : PaletteLayer < 1.5 ? BackgroundHighlights.rgb : BorderHighlights.rgb;
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
