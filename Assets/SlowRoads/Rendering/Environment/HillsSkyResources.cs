using UnityEngine;

namespace SlowRoads.Rendering.Environment
{
    // One mesh and one property block per camera. The source material remains an asset reference.
    public sealed class HillsSkyResources : System.IDisposable
    {
        public Mesh Mesh {get;private set;}
        public Material Material {get;}
        public MaterialPropertyBlock Properties {get;}=new();
        static readonly int FogA=Shader.PropertyToID("_HillsFogA"),FogB=Shader.PropertyToID("_HillsFogB"),FogC=Shader.PropertyToID("_HillsFogC"),
            Range=Shader.PropertyToID("_HillsFogRange"),Haze=Shader.PropertyToID("_HillsHaze"),Origin=Shader.PropertyToID("_HillsLogicalOrigin");
        public HillsSkyResources(Material material)
        {
            Material=material;
            Mesh=new Mesh{name="Hills sky plane",hideFlags=HideFlags.DontSave};
            Mesh.vertices=new[]{new Vector3(-.5f,.5f,0),new Vector3(.5f,.5f,0),new Vector3(-.5f,-.5f,0),new Vector3(.5f,-.5f,0)};
            Mesh.triangles=new[]{0,2,1,2,3,1};Mesh.RecalculateBounds();
        }
        public Matrix4x4 Bind(HillsEnvironmentState state)
        {
            BindAtmosphere(Properties,state);
            // Three camera looks down -Z; Unity camera looks down +Z. Preserve the source plane's distance and extents.
            float range=state.View switch{0=>360,1=>960,2=>1680,3=>3000,4=>4000,_=>5000};
            return state.Camera.transform.localToWorldMatrix*Matrix4x4.TRS(new Vector3(0,0,range-.1f),Quaternion.identity,new Vector3(1e6f,1e5f,1e5f));
        }
        public static void BindAtmosphere(MaterialPropertyBlock properties,HillsEnvironmentState state)
        {
            var p=state.Profile;
            // Values are already linear. SetColor would apply Unity's sRGB transfer a second time.
            properties.SetVector(FogA,(Vector4)HillsEnvironmentProfile.LinearColour(p.Fog.A));properties.SetVector(FogB,(Vector4)HillsEnvironmentProfile.LinearColour(p.Fog.B));properties.SetVector(FogC,(Vector4)HillsEnvironmentProfile.LinearColour(p.Fog.C));
            properties.SetVector(Range,new Vector4(state.FogNear,state.FogFar,state.FogDensity,state.ExponentialFog?1:0));
            properties.SetVector(Haze,new Vector4(state.HazeHeight,state.HazeIntensity,0,0));properties.SetVector(Origin,new Vector4((float)state.OriginX,0,(float)state.OriginZ,0));
        }
        public void Dispose(){if(Mesh==null)return;if(Application.isPlaying)Object.Destroy(Mesh);else Object.DestroyImmediate(Mesh);Mesh=null;}
    }
}
