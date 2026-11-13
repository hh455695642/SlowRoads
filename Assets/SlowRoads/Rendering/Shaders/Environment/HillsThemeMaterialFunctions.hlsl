#ifndef HILLS_THEME_MATERIAL_FUNCTIONS_INCLUDED
#define HILLS_THEME_MATERIAL_FUNCTIONS_INCLUDED
// Include after UnityPerMaterial. Runtime uniform switch: zero additional shader variants.
float3 HillsMaterialLightDirection(float3 fallback)
{
    return _ThemeLightingEnabled > .5 ? float3(0,1,0) : fallback;
}
float3 HillsMaterialLightColour(float3 fallback)
{
    return _ThemeLightingEnabled > .5 ? _ThemeSun.rgb : fallback;
}
float3 HillsMaterialAmbientColour(float3 normal, float3 fallback)
{
    return _ThemeLightingEnabled > .5 ? _ThemeAmbient.rgb + _ThemeHemisphere.rgb * (normal.y * .5 + .5) : fallback;
}
#endif
