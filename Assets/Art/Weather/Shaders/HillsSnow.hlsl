#ifndef HILLS_SNOW_INCLUDED
#define HILLS_SNOW_INCLUDED
// Source Settings ni/hi/oi/ri, exterior minDist=.3; vehicle headlight branch excluded.
TEXTURE2D(_SnowNoise);SAMPLER(sampler_Linear_Repeat);
float4x4 _SnowModelView,_SnowProjection;
float4 _SnowOffset,_SnowParameters; // application time, speed, minDist, source pixel-height/2
float4 _SnowScreen,_SnowColour; // width, height, native point range min/max
float SnowMod(float x,float y){return x-y*floor(x/y);}
struct HillsSnowPoint {float3 transformed;float4 clip;float size;float depth;float skip;};
HillsSnowPoint HillsSnowSimulate(float3 position)
{
    HillsSnowPoint o;float time=_SnowParameters.x;
    float noise=SAMPLE_TEXTURE2D_LOD(_SnowNoise,sampler_Linear_Repeat,position.xz/50+time/200,0).r;
    o.transformed.y=SnowMod(SnowMod(position.y-_SnowOffset.y,30)-time*_SnowParameters.y,30);
    o.transformed.x=SnowMod(position.x-_SnowOffset.x,30)+noise*2;
    o.transformed.z=SnowMod(position.z-_SnowOffset.z,30)+cos(noise);
    float4 view=mul(_SnowModelView,float4(o.transformed,1));o.clip=mul(_SnowProjection,view);o.depth=-view.z;
    o.size=.045*_SnowParameters.w/o.depth;o.skip=o.depth<_SnowParameters.z?1:0;return o;
}
struct HillsSnowPrepared {float4 clip;float4 info;}; // size, visible, view depth, reserved
#endif
