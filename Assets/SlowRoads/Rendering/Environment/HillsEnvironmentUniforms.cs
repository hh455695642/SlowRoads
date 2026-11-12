using UnityEngine;
using UnityEngine.Rendering;

namespace SlowRoads.Rendering.Environment
{
    // One immutable per-camera upload. No Material writes, per-instance traversal, SH rebuild or RT.
    public readonly struct HillsEnvironmentUniforms
    {
        public readonly Vector4 Control,FogA,FogB,FogC,Range,Haze,Origin,Sun,Ambient,Hemi,Theme,WaterBase,WaterBody,WaterHighlight;
        static readonly int C=Shader.PropertyToID("_HillsSurfaceControl"),A=Shader.PropertyToID("_HillsFogA"),B=Shader.PropertyToID("_HillsFogB"),F=Shader.PropertyToID("_HillsFogC"),R=Shader.PropertyToID("_HillsFogRange"),H=Shader.PropertyToID("_HillsHaze"),O=Shader.PropertyToID("_HillsLogicalOrigin"),S=Shader.PropertyToID("_HillsSun"),I=Shader.PropertyToID("_HillsAmbient"),E=Shader.PropertyToID("_HillsHemi"),T=Shader.PropertyToID("_HillsTheme"),W0=Shader.PropertyToID("_HillsWaterBase"),W1=Shader.PropertyToID("_HillsWaterBody"),W2=Shader.PropertyToID("_HillsWaterHighlight");
        public HillsEnvironmentUniforms(HillsEnvironmentState state)
        {
            this=default;if(state?.Profile==null)return;var p=state.Profile;var l=p.Lights;
            Control=new Vector4(state.AtmosphereVisible?1:0,state.LightingEnabled?1:0,l.SunIntensity,state.Revision);
            FogA=(Vector4)HillsEnvironmentProfile.LinearColour(p.Fog.A);FogB=(Vector4)HillsEnvironmentProfile.LinearColour(p.Fog.B);FogC=(Vector4)HillsEnvironmentProfile.LinearColour(p.Fog.C);
            Range=new Vector4(state.FogNear,state.FogFar,state.FogDensity,state.ExponentialFog?1:0);Haze=new Vector4(state.HazeHeight,state.HazeIntensity,0,0);Origin=new Vector4((float)state.OriginX,0,(float)state.OriginZ,0);
            // Original Three r155 non-legacy irradiance -> Lambert / standard diffuse 1/pi.
            // Ni's t has NO lower clamp; weak sun can increase ambient under observer shadow.
            float intensity=(float)(l.AmbientIntensity*(1-.2*state.ObserverShadow*System.Math.Min(1,(l.SunIntensity-3)/2d)));
            Color ambient=HillsEnvironmentProfile.LinearColour(l.Ambient);
            if(l.AmbientShadow!=0)ambient=Color.LerpUnclamped(ambient,HillsEnvironmentProfile.LinearColour(5471792),(float)state.ObserverShadow);
            Sun=(Vector4)(HillsEnvironmentProfile.LinearColour(l.Sun)*(l.SunIntensity/Mathf.PI));
            Ambient=(Vector4)(ambient*(intensity/Mathf.PI));Hemi=(Vector4)(HillsEnvironmentProfile.LinearColour(l.Sun)*(intensity/Mathf.PI));
            Theme=new Vector4(p.ShadowFactor,l.Radiance,l.Fresnel,p.TreeDiscolouration);
            WaterBase=(Vector4)HillsEnvironmentProfile.LinearColour(p.WaterBase);WaterBody=(Vector4)HillsEnvironmentProfile.LinearColour(p.WaterBody);WaterHighlight=(Vector4)HillsEnvironmentProfile.LinearColour(p.WaterHighlight);
        }
        public static void Clear()
        {
            foreach(var id in new[]{C,A,B,F,R,H,O,S,I,E,T,W0,W1,W2})Shader.SetGlobalVector(id,Vector4.zero);
        }
        public void Record(CommandBuffer cmd)
        {
            cmd.SetGlobalVector(C,Control);cmd.SetGlobalVector(A,FogA);cmd.SetGlobalVector(B,FogB);cmd.SetGlobalVector(F,FogC);cmd.SetGlobalVector(R,Range);cmd.SetGlobalVector(H,Haze);cmd.SetGlobalVector(O,Origin);
            cmd.SetGlobalVector(S,Sun);cmd.SetGlobalVector(I,Ambient);cmd.SetGlobalVector(E,Hemi);cmd.SetGlobalVector(T,Theme);cmd.SetGlobalVector(W0,WaterBase);cmd.SetGlobalVector(W1,WaterBody);cmd.SetGlobalVector(W2,WaterHighlight);
        }
    }
}
