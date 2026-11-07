using System;
using SlowRoads.Generation.Terrain;

namespace SlowRoads.Generation.Contents.Water
{
    /// <summary>Frozen Dt.genHeights0 inputs. No Pl queries and no fine-height overwrite.</summary>
    public sealed class WaterCoarseSnapshot
    {
        readonly double[] heights;
        public readonly int Size,Resolution;
        public readonly bool HasSea;
        public WaterCoarseSnapshot(int size,int resolution,double[] values,bool hasSea)
        {Size=size;Resolution=resolution;heights=(double[])values.Clone();HasSea=hasSea;}
        // Dt.lookupHeight Settings L43017–43040, coarse branch, original operation order.
        public double Lookup(double x,double z)
        {
            if(x<0||z<0||x>Size||z>Size||!double.IsFinite(x+z))throw new ArgumentOutOfRangeException();
            int f=Resolution,k=f+1;double dx=x/Size*f,dz=z/Size*f;int xi=(int)Math.Floor(dx),zi=(int)Math.Floor(dz);
            if(xi==f){xi--;dx=1;}else dx-=xi;if(zi==f){zi--;dz=1;}else dz-=zi;
            dx/=2;dz/=2;if(xi%2==1){xi--;dx+=.5;}if(zi%2==1){zi--;dz+=.5;}
            int tl=xi*k+zi,bl=tl+k*2;
            double a=heights[tl]*(1-dz)+heights[tl+2]*dz,b=heights[bl]*(1-dz)+heights[bl+2]*dz;return a*(1-dx)+b*dx;
        }
    }
    /// <summary>Immutable published water channels. OriginalDepth is signed raw elevation, not positive water depth.</summary>
    public sealed class WaterTileData
    {
        public readonly TerrainRegionKey Key;
        public readonly long TerrainRevision,LayerRevision;
        public readonly double OriginX,OriginZ;
        public readonly int Size,Segments;
        public readonly bool HasSea;
        public TerrainBuffer<float> Positions {get;}public TerrainBuffer<float> Normals {get;}public TerrainBuffer<float> UV {get;}
        public TerrainBuffer<float> OriginalDepth {get;}public TerrainBuffer<int> Indices {get;}
        internal WaterTileData(TerrainRegionKey key,long revision,long layer,double x,double z,int size,int n,bool sea,float[] p,float[] normals,float[] uv,float[] depth,int[] indices)
        {Key=key;TerrainRevision=revision;LayerRevision=layer;OriginX=x;OriginZ=z;Size=size;Segments=n;HasSea=sea;
            Positions=new(p);Normals=new(normals);UV=new(uv);OriginalDepth=new(depth);Indices=new(indices);}
    }
    /// <summary>i7.genCustom0: one X column per unit, Z inner loop. No random consumption.</summary>
    public sealed class WaterTileBuilder
    {
        readonly TerrainRegionKey key;readonly long revision,layer;readonly double ox,oz;
        readonly WaterCoarseSnapshot source;readonly int n,k;
        readonly float[] positions,normals,uv,depth;readonly int[] indices;int column;
        public WaterTileData Data {get;private set;}
        public static int Segments(int size,int detail)=>Math.Max(1,(int)Math.Floor(new[]{.2,.3,.4,.5,.5}[detail]*(size/20.0)));
        public WaterTileBuilder(TerrainRegionKey key,long revision,long layer,double x,double z,WaterCoarseSnapshot source,int segments)
        {
            if(segments<1)throw new ArgumentOutOfRangeException(nameof(segments));this.key=key;this.revision=revision;this.layer=layer;ox=x;oz=z;this.source=source;n=segments;k=n+1;
            int count=source.HasSea?k*k:0;positions=new float[count*3];normals=new float[count*3];uv=new float[count*2];depth=new float[count];indices=new int[source.HasSea?n*n*6:0];
        }
        public int Advance(int budget)
        {
            if(budget<0)throw new ArgumentOutOfRangeException(nameof(budget));int used=0;
            while(used<budget&&Data==null)
            {
                used++;
                if(source.HasSea)for(int z=0;z<k;z++)
                {
                    int i=z*k+column;double step=(double)source.Size/n;
                    // Preserve original PlaneGeometry Float32 write BEFORE the mesh centre translation.
                    positions[i*3]=(float)((float)(column*step-source.Size/2.0)+source.Size/2.0);
                    positions[i*3+2]=(float)(-(float)(-(z*step-source.Size/2.0))+source.Size/2.0);
                    normals[i*3+1]=1;uv[i*2]=(float)((double)column/n);uv[i*2+1]=(float)(1-(double)z/n);
                    depth[i]=n<=1?0:(float)source.Lookup((double)column/n*source.Size,(double)z/n*source.Size);
                    if(column<n&&z<n){int at=(z*n+column)*6;indices[at]=i;indices[at+1]=i+k;indices[at+2]=i+1;indices[at+3]=i+k;indices[at+4]=i+k+1;indices[at+5]=i+1;}
                }
                if(!source.HasSea||++column==k)Data=new WaterTileData(key,revision,layer,ox,oz,source.Size,n,source.HasSea,positions,normals,uv,depth,indices);
            }
            return used;
        }
    }
}
