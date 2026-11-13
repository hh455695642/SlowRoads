using UnityEngine;

namespace SlowRoads.Environment
{
    public sealed class EnvironmentCloudResources : System.IDisposable
    {
        public Mesh Mesh {get;private set;}
        public Material Material {get;}
        public MaterialPropertyBlock Properties {get;}=new();
        public int Segments {get;private set;}=-1;
        public int MeshBuilds {get;private set;}
        static readonly int Shelf=Shader.PropertyToID("_HillsCloudShelf"),Parameters=Shader.PropertyToID("_HillsCloudParameters"),
            Highlight=Shader.PropertyToID("_HillsCloudHighlight"),Lowlight=Shader.PropertyToID("_HillsCloudLowlight"),ModelView=Shader.PropertyToID("_HillsCloudModelView");
        public EnvironmentCloudResources(Material material){Material=material;}
        public void Configure(int segments)
        {
            if(segments<1||segments>128)throw new System.ArgumentOutOfRangeException(nameof(segments));if(Segments==segments)return;
            // Source PlaneGeometry: CPU double construction, one float conversion, original row/index order.
            int width=segments+1;var positions=new Vector3[width*width];var uv=new Vector2[positions.Length];var indices=new int[segments*segments*6];
            double step=1d/segments;
            for(int y=0;y<width;y++)for(int x=0;x<width;x++)
            {int i=y*width+x;positions[i]=new Vector3((float)(x*step-.5),(float)(-(y*step-.5)),0);uv[i]=new Vector2((float)((double)x/segments),(float)(1d-(double)y/segments));}
            int at=0;for(int y=0;y<segments;y++)for(int x=0;x<segments;x++)
            {int a=x+width*y,b=x+width*(y+1),c=x+1+width*(y+1),d=x+1+width*y;indices[at++]=a;indices[at++]=b;indices[at++]=d;indices[at++]=b;indices[at++]=c;indices[at++]=d;}
            ReleaseMesh();Mesh=new Mesh{name="Hills cloud plane",hideFlags=HideFlags.DontSave};Mesh.vertices=positions;Mesh.uv=uv;Mesh.triangles=indices;Mesh.RecalculateBounds();Segments=segments;MeshBuilds++;
        }
        public Matrix4x4 Bind(in EnvironmentFrameSettings state,Camera camera)
        {
            // Share the source fog contract; no material cloning and no per-vertex CPU animation.
            EnvironmentSkyResources.BindAtmosphere(Properties,state);
            Properties.SetVector(Shelf,state.CloudShelf);
            Properties.SetVector(Parameters,new Vector4(state.CloudGeometry.x,state.CloudMode,(float)state.Input.CloudTime,0));
            Properties.SetVector(Highlight,state.CloudHighlight);Properties.SetVector(Lowlight,state.CloudLowlight);
            var cameraPosition=camera.transform.position;
            // Exact source +90° basis avoids Quaternion float residue across a 22km plane.
            var matrix=Matrix4x4.zero;matrix.m00=state.CloudGeometry.y;matrix.m21=state.CloudGeometry.y;matrix.m12=-1;
            matrix.m03=cameraPosition.x;matrix.m13=state.CloudGeometry.x;matrix.m23=cameraPosition.z;matrix.m33=1;
            Properties.SetMatrix(ModelView,camera.worldToCameraMatrix*matrix);return matrix;
        }
        void ReleaseMesh(){if(Mesh==null)return;if(Application.isPlaying)Object.Destroy(Mesh);else Object.DestroyImmediate(Mesh);Mesh=null;}
        public void Dispose()=>ReleaseMesh();
    }
}
