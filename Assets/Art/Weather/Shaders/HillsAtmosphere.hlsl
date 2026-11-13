#ifndef HILLS_ATMOSPHERE_INCLUDED
#define HILLS_ATMOSPHERE_INCLUDED
// Original Dev we/Ie/Ae/Me. Colour math uses float for the frozen linear oracle.
// Uniform toggles avoid season x time x weather shader variants. No texture or RT reads.
float4 _HillsFogA,_HillsFogB,_HillsFogC;
float4 _HillsFogRange; // near, far, density, EXP2
float4 _HillsHaze; // height, intensity, reserved, reserved
float4 _HillsLogicalOrigin;
float HillsFogRand(float2 uv)
{
    float dt=dot(uv,float2(12.9898,78.233));
    float sn=dt-3.141592653589793*floor(dt/3.141592653589793);
    return frac(sin(sn)*43758.5453);
}
float HillsFogNoise(float2 p,float freq)
{
    float unit=1024/freq;float2 ij=floor(p/unit),xy=frac(p/unit);
    xy=.5*(1-cos(3.141592653589793*xy));
    float a=HillsFogRand(ij),b=HillsFogRand(ij+float2(1,0));
    float c=HillsFogRand(ij+float2(0,1)),d=HillsFogRand(ij+float2(1,1));
    return lerp(lerp(a,b,xy.x),lerp(c,d,xy.x),xy.y);
}
float HillsFogPNoise(float2 p)
{
    float n=0,norm=0,f=4,amp=1;
    // Source pNoise(p,3) breaks after iCount==3: four iterations, not three.
    [unroll]for(int i=0;i<4;i++){n+=amp*HillsFogNoise(p,f);f*=2;norm+=amp;amp*=.5;}
    float nf=n/norm;return nf*nf*nf*nf;
}
// x=depth, y=haze, z=desaturation, w=horizon blend. These are vertex varyings in the original.
float4 HillsFogVertex(float3 world,float3 camera,bool water)
{
    float near=water?0:_HillsFogRange.x,far=_HillsFogRange.y,height=_HillsHaze.x,intensity=_HillsHaze.y;
    float depth=distance(world,camera),haze=min(1,depth/far),desat=haze;
    haze*=haze;haze*=1+min(0,intensity);desat*=1+min(0,intensity);
    float fog;
    if(_HillsFogRange.w>.5){float d=max(0,(depth-near)*(far/(far-near)));fog=1-exp(-_HillsFogRange.z*_HillsFogRange.z*d*d);}
    else fog=smoothstep(near,far,depth);
    float fogHeight=world.y;
    if(_HillsFogRange.w>.5)fogHeight+=HillsFogPNoise((world.xz+_HillsLogicalOrigin.xz)/2)*height*min(1,depth/1000);
    float test=min(camera.y,fogHeight);
    if(height>0&&test<height)
    {
        float h=(1-test/height)*intensity;
        if(depth<50){float v=1-depth/50;h*=1-v*v;}
        fog+=max(0,h);
    }
    else if(height<0)
    {
        float h=min(1,max(camera.y,fogHeight)/-height)*intensity;
        if(depth<h*100){float v=1-depth/(h*100);h*=1-v*v;}
        fog+=h;
    }
    float avg=(far+near)/2,blend=1;
    if(avg<500)blend=avg<50?0:(avg-50)/450;
    if(height<0)blend*=1-intensity;
    return float4(min(1,fog),haze,desat,blend);
}
float3 HillsFogFragment(float3 colour,float3 world,float3 camera,float4 fog)
{
    float blend=max((world.y-camera.y)*2/distance(world,camera),0)*fog.w;
    colour=lerp(colour,dot(colour,float3(.299,.587,.114)).xxx,fog.z);
    colour=lerp(colour,_HillsFogC.rgb,fog.y);
    return lerp(colour,lerp(_HillsFogA.rgb,_HillsFogB.rgb,blend),fog.x);
}
#endif
