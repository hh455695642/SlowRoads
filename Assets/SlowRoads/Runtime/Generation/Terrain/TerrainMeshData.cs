using System;
using System.Collections;
using System.Collections.Generic;
using SlowRoads.Generation.Midline;
using SlowRoads.Generation.Streaming;

namespace SlowRoads.Generation.Terrain
{
    // A borrowed read-only view. Release invalidates even views acquired before Dispose;
    // no array or writable Memory escapes into a consumer or buffer pool.
    public sealed class TerrainBuffer<T> : IReadOnlyList<T>
    {
        T[] values;
        internal TerrainBuffer(T[] values){this.values=values??throw new ArgumentNullException(nameof(values));}
        void Check(){if(values==null)throw new ObjectDisposedException(nameof(TerrainBuffer<T>));}
        public int Length {get{Check();return values.Length;}}
        public int Count=>Length;
        public T this[int index] {get{Check();return values[index];}}
        public T[] ToArray(){Check();return (T[])values.Clone();}
        public void CopyTo(T[] destination){Check();Array.Copy(values,destination,values.Length);}
        internal void Release(){values=null;}
        public IEnumerator<T> GetEnumerator(){for(int i=0;i<Length;i++)yield return this[i];}
        IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
    }

    public readonly struct TerrainRegionKey : IEquatable<TerrainRegionKey>
    {
        public readonly int Generation,X,Z;
        public readonly long CollectionEpoch;
        public readonly bool IsChunk;
        public readonly string Configuration;
        internal TerrainRegionKey(int generation,string configuration,bool chunk,int x,int z,long epoch)
        {Generation=generation;Configuration=configuration;IsChunk=chunk;X=x;Z=z;CollectionEpoch=epoch;}
        public bool Equals(TerrainRegionKey b)=>Generation==b.Generation&&Configuration==b.Configuration&&IsChunk==b.IsChunk&&X==b.X&&Z==b.Z&&CollectionEpoch==b.CollectionEpoch;
        public override bool Equals(object value)=>value is TerrainRegionKey k&&Equals(k);
        public override int GetHashCode()=>HashCode.Combine(Generation,Configuration,IsChunk,X,Z,CollectionEpoch);
    }
    public readonly struct TerrainWorldBounds
    {
        public readonly Double3 Min,Max;
        internal TerrainWorldBounds(Double3 min,Double3 max){Min=min;Max=max;}
    }
    public sealed class TerrainCellDescriptor
    {
        public readonly int X,Z,VertexStart,CoarseIndexStart,FineIndexStart,Resolution;
        public readonly bool Inner;
        public IReadOnlyList<int> Candidates {get;}
        internal TerrainCellDescriptor(NearTerrainCell cell,int vertex,int coarse,int fine,int resolution)
        {X=cell.X;Z=cell.Z;Inner=cell.Inner;VertexStart=vertex;CoarseIndexStart=coarse;FineIndexStart=fine;Resolution=resolution;Candidates=Array.AsReadOnly(cell.Candidates());}
        // Boundary vertex address, X-major/Z-minor, including the shared end point.
        public int Vertex(int x,int z)
        {if((uint)x>Resolution||(uint)z>Resolution)throw new ArgumentOutOfRangeException();return VertexStart+x*(Resolution+1)+z;}
    }
    /// <summary>Successful synchronous upload receipt; owns no CPU arrays or road nodes.
    /// T4 may retain this until coverage hand-off, then Retire. Ordinary road Advance
    /// does not invalidate baked geometry. Layer/generation/owner changes do.</summary>
    public sealed class TerrainResidency
    {
        readonly RoadStreamingSession owner;
        public readonly TerrainRegionKey Key;
        // Retain value metadata after near-field CPU arrays are released. T6 and
        // consumers can inspect a committed region without reopening road data.
        public readonly TerrainWorldBounds WorldBounds;
        public readonly long Revision,LayerRevision;
        public readonly int FirstSupportIndex,LastSupportIndex,CoarseCount,FineStart,FineCount;
        bool retired;
        internal TerrainResidency(RoadStreamingSession owner,TerrainMeshData data)
        {this.owner=owner;Key=data.Key;WorldBounds=data.WorldBounds;Revision=data.Revision;LayerRevision=data.LayerRevision;
            FirstSupportIndex=data.FirstSupportIndex;LastSupportIndex=data.LastSupportIndex;CoarseCount=data.CoarseCount;FineStart=data.FineStart;FineCount=data.FineCount;}
        public bool IsCurrent=>!retired&&owner.IsTerrainDataCurrent(Key.Generation,0,LayerRevision,false);
        public void Retire(){retired=true;}
    }

    /// <summary>Completed immutable CPU geometry. Builder transfers arrays once; Data
    /// owns them until Dispose. Mesh/its residency belong to the uploader, independently.
    /// Source: f0/U/Dt Settings L41004–41447, L42616–43040.</summary>
    public sealed class TerrainMeshData : IDisposable
    {
        internal readonly RoadStreamingSession Owner;
        public readonly int Generation;
        public readonly long Revision,LayerRevision,ResidencyRevision;
        public readonly int FirstSupportIndex,LastSupportIndex;
        public readonly TerrainGenerationConfig Config;
        public readonly bool RoadDependent;
        public readonly double OriginX,OriginZ;
        public readonly int CoarseCount,FineStart,FineCount;
        public TerrainRegionKey Key {get;}
        public TerrainWorldBounds WorldBounds {get;}
        public IReadOnlyList<TerrainCellDescriptor> Cells {get;}
        public IReadOnlyList<(int x,int z)> ParentTiles {get;}
        public TerrainBuffer<float> Positions {get;}
        public TerrainBuffer<float> Normals {get;}
        public TerrainBuffer<float> CoarseNormals {get;}
        public TerrainBuffer<float> RoadProximity {get;}
        public TerrainBuffer<float> RoadUv {get;}
        public TerrainBuffer<float> Curvature {get;}
        public TerrainBuffer<sbyte> Overlap {get;}
        public TerrainBuffer<int> Indices {get;}
        public TerrainBuffer<double> Heights {get;}
        public TerrainBuffer<bool> CoarseHeightValid {get;}
        public TerrainBuffer<bool> CoarseNormalValid {get;}
        public bool IsDisposed {get;private set;}
        public int VertexCount=>IsDisposed?0:Positions.Length/3;
        internal TerrainMeshData(RoadStreamingSession owner,TerrainBuildContext context,bool roads,double x,double z,
            float[] positions,float[] normals,float[] coarseNormals,int[] indices,int coarseCount,int fineStart,int fineCount,
            float[] proximity=null,float[] uv=null,NearTerrainChunkLayout layout=null,double[] heights=null,bool[] heightValid=null,bool[] normalValid=null)
        {
            Owner=owner;Generation=context.Generation;Revision=context.Revision;LayerRevision=context.LayerRevision;RoadDependent=roads;
            Config=context.Config;FirstSupportIndex=context.FirstSupportIndex;LastSupportIndex=context.LastSupportIndex;
            ResidencyRevision=context.Window?.Residency??-1;
            OriginX=x;OriginZ=z;Positions=new(positions);Normals=new(normals);CoarseNormals=new(coarseNormals);Indices=new(indices);
            CoarseCount=coarseCount;FineStart=fineStart;FineCount=fineCount;
            if(proximity!=null)RoadProximity=new(proximity);if(uv!=null)RoadUv=new(uv);
            Curvature=new(new float[positions.Length/3]);if(!roads)Overlap=new(new sbyte[positions.Length/3]);
            if(heights!=null){Heights=new(heights);CoarseHeightValid=new(heightValid);CoarseNormalValid=new(normalValid);}
            Key=new(Generation,Config.Identity,roads,roads?layout.Id:(int)(x/Config.TileSize),roads?0:(int)(z/Config.TileSize),layout?.CollectionEpoch??0);
            var cells=new List<TerrainCellDescriptor>();var parents=new List<(int,int)>();
            if(layout!=null)
            {
                int inner=0;foreach(var c in layout.Cells)if(c.Inner)inner++;
                int vc=(Config.CellResolution+1)*(Config.CellResolution+1),ic=coarseCount/layout.Count;
                int fc=Config.CellResolution*Config.CellResolution*6,ci=0,co=inner*ic,fi=coarseCount;
                for(int i=0;i<layout.Count;i++){var c=layout.Cells[i];cells.Add(new(c,i*vc,c.Inner?ci:co,c.Inner?fi:-1,Config.CellResolution));if(c.Inner){ci+=ic;fi+=fc;}else co+=ic;}
                foreach(var p in layout.ParentTiles)parents.Add(p);
                parents.Sort((a,b)=>a.Item1!=b.Item1?a.Item1.CompareTo(b.Item1):a.Item2.CompareTo(b.Item2));
            }
            Cells=cells.AsReadOnly();ParentTiles=parents.AsReadOnly();
            double minX=double.MaxValue,minY=minX,minZ=minX,maxX=double.MinValue,maxY=maxX,maxZ=maxX;
            for(int i=0;i<positions.Length;i+=3){minX=Math.Min(minX,x+positions[i]);maxX=Math.Max(maxX,x+positions[i]);minY=Math.Min(minY,positions[i+1]);maxY=Math.Max(maxY,positions[i+1]);minZ=Math.Min(minZ,z+positions[i+2]);maxZ=Math.Max(maxZ,z+positions[i+2]);}
            WorldBounds=new(new Double3(minX,minY,minZ),new Double3(maxX,maxY,maxZ));
        }
        public bool CanPublish(RoadStreamingSession session)=>!IsDisposed&&ReferenceEquals(Owner,session)&&session.IsTerrainDataCurrent(Generation,Revision,LayerRevision,RoadDependent)&&(!RoadDependent||session.Spatial.ResidencyRevision==ResidencyRevision);
        // Called only after the GPU copy succeeds, in the same session-thread boundary.
        // This creates no delayed publication queue and does not relax CanPublish.
        public TerrainResidency CommitUpload(RoadStreamingSession session)
        {if(!CanPublish(session))throw new InvalidOperationException("Stale terrain publication");return new TerrainResidency(session,this);}
        public double LookupHeight(double worldX,double worldZ,bool fine=true)
        {
            if(IsDisposed)throw new ObjectDisposedException(nameof(TerrainMeshData));
            if(Heights==null)throw new InvalidOperationException("Only far tiles expose original Dt height lookup");
            int f=Config.TileFineResolution,z=f+1;
            double dx=(worldX-OriginX)/Config.TileSize*f,dz=(worldZ-OriginZ)/Config.TileSize*f;
            if(!double.IsFinite(dx)||!double.IsFinite(dz)||dx<0||dz<0||dx>f||dz>f)throw new ArgumentOutOfRangeException();
            int xi=(int)Math.Floor(dx),zi=(int)Math.Floor(dz);
            if(xi==f){xi--;dx=1;}else dx-=xi;if(zi==f){zi--;dz=1;}else dz-=zi;
            int step=fine?1:2;
            if(!fine){dx/=2;dz/=2;if(xi%2==1){xi--;dx+=.5;}if(zi%2==1){zi--;dz+=.5;}}
            int tl=xi*z+zi,bl=tl+z*step;
            return (Heights[tl]*(1-dz)+Heights[tl+step]*dz)*(1-dx)+(Heights[bl]*(1-dz)+Heights[bl+step]*dz)*dx;
        }
        public void Dispose()
        {
            if(IsDisposed)return;IsDisposed=true;
            Positions.Release();Normals.Release();CoarseNormals.Release();Indices.Release();Curvature.Release();
            RoadProximity?.Release();RoadUv?.Release();Overlap?.Release();Heights?.Release();CoarseHeightValid?.Release();CoarseNormalValid?.Release();
        }
    }
}
