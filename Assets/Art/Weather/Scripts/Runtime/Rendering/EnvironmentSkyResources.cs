using UnityEngine;

namespace SlowRoads.Environment
{
    // One mesh and one property block per camera. The source material remains an asset reference.
    public sealed class EnvironmentSkyResources : System.IDisposable
    {
        public Mesh Mesh {get;private set;}
        public Material Material {get;}
        public MaterialPropertyBlock Properties {get;}=new();
        static readonly int FogA=Shader.PropertyToID("_HillsFogA"),FogB=Shader.PropertyToID("_HillsFogB"),FogC=Shader.PropertyToID("_HillsFogC"),
            Range=Shader.PropertyToID("_HillsFogRange"),Haze=Shader.PropertyToID("_HillsHaze"),Origin=Shader.PropertyToID("_HillsLogicalOrigin");
        public EnvironmentSkyResources(Material material)
        {
            Material=material;
            Mesh=new Mesh{name="Hills sky plane",hideFlags=HideFlags.DontSave};
            Mesh.vertices=new[]{new Vector3(-.5f,.5f,0),new Vector3(.5f,.5f,0),new Vector3(-.5f,-.5f,0),new Vector3(.5f,-.5f,0)};
            Mesh.triangles=new[]{0,2,1,2,3,1};Mesh.RecalculateBounds();
        }
        public Matrix4x4 Bind(in EnvironmentFrameSettings state, Camera camera)
        {
            BindAtmosphere(Properties,state);
            // Three camera looks down -Z; Unity camera looks down +Z. Preserve the source plane's distance and extents.
            float range=state.BackgroundDistance;
            return camera.transform.localToWorldMatrix*Matrix4x4.TRS(new Vector3(0,0,range-.1f),Quaternion.identity,new Vector3(1e6f,1e5f,1e5f));
        }
        public static void BindAtmosphere(MaterialPropertyBlock properties,in EnvironmentFrameSettings state)
        {
            // Values are already linear. SetColor would apply Unity's sRGB transfer a second time.
            var fog=state.Sky;
            properties.SetVector(FogA,fog.A);properties.SetVector(FogB,fog.B);properties.SetVector(FogC,fog.C);
            properties.SetVector(Range,fog.Range);properties.SetVector(Haze,fog.Haze);
            properties.SetVector(Origin,new Vector4((float)state.Input.OriginX,0,(float)state.Input.OriginZ,0));
        }
        public void Dispose(){if(Mesh==null)return;if(Application.isPlaying)Object.Destroy(Mesh);else Object.DestroyImmediate(Mesh);Mesh=null;}
    }
}
