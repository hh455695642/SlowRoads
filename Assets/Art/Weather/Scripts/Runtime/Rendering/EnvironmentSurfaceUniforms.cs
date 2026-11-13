using UnityEngine;
using UnityEngine.Rendering;

namespace SlowRoads.Environment
{
    // Weather-only camera uniforms. Theme light is owned by the host's materials.
    public readonly struct EnvironmentSurfaceUniforms
    {
        readonly Vector4 control,a,b,c,range,haze,origin;
        static readonly int C=Shader.PropertyToID("_HillsSurfaceControl"),A=Shader.PropertyToID("_HillsFogA"),B=Shader.PropertyToID("_HillsFogB"),F=Shader.PropertyToID("_HillsFogC"),R=Shader.PropertyToID("_HillsFogRange"),H=Shader.PropertyToID("_HillsHaze"),O=Shader.PropertyToID("_HillsLogicalOrigin");
        public EnvironmentSurfaceUniforms(in EnvironmentFrameSettings frame,int revision)
        {
            control=new(frame.Fog.Enabled?1:0,0,0,revision);
            a=frame.Fog.A;b=frame.Fog.B;c=frame.Fog.C;range=frame.Fog.Range;haze=frame.Fog.Haze;
            origin=new((float)frame.Input.OriginX,0,(float)frame.Input.OriginZ,0);
        }
        public void Record(CommandBuffer cmd)
        {
            cmd.SetGlobalVector(C,control);cmd.SetGlobalVector(A,a);cmd.SetGlobalVector(B,b);cmd.SetGlobalVector(F,c);cmd.SetGlobalVector(R,range);cmd.SetGlobalVector(H,haze);cmd.SetGlobalVector(O,origin);
        }
        public static void Clear()
        {
            Shader.SetGlobalVector(C,Vector4.zero);Shader.SetGlobalVector(A,Vector4.zero);Shader.SetGlobalVector(B,Vector4.zero);Shader.SetGlobalVector(F,Vector4.zero);Shader.SetGlobalVector(R,Vector4.zero);Shader.SetGlobalVector(H,Vector4.zero);Shader.SetGlobalVector(O,Vector4.zero);
        }
    }
}
