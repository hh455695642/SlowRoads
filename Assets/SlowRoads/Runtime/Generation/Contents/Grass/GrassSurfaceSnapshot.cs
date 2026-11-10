using System;
using SlowRoads.Generation.Midline;
using SlowRoads.Generation.Terrain;
using SlowRoads.Generation.Streaming;
using SlowRoads.Generation.Contents.Walls;
using SlowRoads.Generation.Contents.Fields;

namespace SlowRoads.Generation.Contents.Grass
{
    /// <summary>Copy a single finished cell before T4 retires its CPU arrays.
    /// Original closestNode is the cell's first registration candidate (At.init L40981),
    /// not a new projection and not the closest visible wall/tree.</summary>
    public sealed class GrassSurfaceSnapshot
    {
        public readonly TerrainRegionKey Owner;public readonly long TerrainRevision,LayerRevision,WallRevision;
        public readonly ContentVersion ContentVersion;public readonly GrassCellInput Input;
        public readonly int RoadIndex;public readonly bool WallsBound;
        readonly TerrainResidency parent;
        GrassSurfaceSnapshot(TerrainMeshData data,TerrainResidency receipt,WallStreamingSession walls,GrassCellInput input,int roadIndex,bool bound)
        {Owner=data.Key;TerrainRevision=data.Revision;LayerRevision=data.LayerRevision;WallRevision=walls.Revision;ContentVersion=walls.ContentVersion;Input=input;parent=receipt;RoadIndex=roadIndex;WallsBound=bound;}
        GrassSurfaceSnapshot(GrassSurfaceSnapshot source,WallStreamingSession walls,GrassCellInput input)
        {Owner=source.Owner;TerrainRevision=source.TerrainRevision;LayerRevision=source.LayerRevision;WallRevision=walls.Revision;ContentVersion=walls.ContentVersion;Input=input;parent=source.parent;RoadIndex=source.RoadIndex;WallsBound=true;}
        public bool ParentCurrent=>parent.IsCurrent;
        public bool IsCurrent(WallStreamingSession walls)=>parent.IsCurrent&&walls.QueryReady&&WallRevision==walls.Revision&&ContentVersion.Equals(walls.ContentVersion);
        internal bool TryBindWalls(WallStreamingSession walls,out GrassSurfaceSnapshot result)
        {
            // T4's receipt protects the frozen local surface. An unrelated far field or
            // road append may advance the GLOBAL content revision while this receipt stays
            // valid; rejecting that stamp would silently lose an already queued grass job.
            // Wall values are read live at its turn, as s0 reads closestNode.l/rWallDist.
            var version=walls.ContentVersion;
            result=null;if(!parent.IsCurrent||ContentVersion.Generation!=version.Generation||ContentVersion.Layer!=version.Layer||!walls.TryGetSides(RoadIndex,out double left,out double right,out int side))return false;
            result=new(this,walls,Input.WithWalls(left,right,side));return true;
        }
        public static bool TryCapture(RoadStreamingSession road,FieldStreamingSession fields,TerrainMeshData data,TerrainResidency receipt,TerrainCellDescriptor cell,
            CoarseNode closest,WallStreamingSession walls,double retireIndex,out GrassSurfaceSnapshot result,bool allowPendingWalls=false)
        {
            result=null;
            if(fields==null||fields.HasQueuedWork||data==null||data.IsDisposed||!data.RoadDependent||receipt==null||!receipt.IsCurrent||walls==null||!data.CanPublish(road)||
                !receipt.Key.Equals(data.Key)||receipt.Revision!=data.Revision||!allowPendingWalls&&!walls.QueryReady)return false;
            bool member=false;foreach(var value in data.Cells)if(ReferenceEquals(value,cell)){member=true;break;}
            if(cell==null||!member||cell.Candidates.Count==0||closest==null||closest.Index!=cell.Candidates[0])return false;
            bool bound=walls.TryGetSides(closest.Index,out var left,out var right,out var wallSide);if(!bound&&!allowPendingWalls)return false;
            int resolution=cell.Resolution;var samples=new GrassSurfaceSample[resolution*resolution];
            for(int x=0;x<resolution;x++)for(int z=0;z<resolution;z++)
            {
                int v=cell.Vertex(x,z);samples[x*resolution+z]=new GrassSurfaceSample(data.Positions[v*3+1],data.RoadProximity[v],data.TreeMask[v],
                    new Double3(data.Normals[v*3],data.Normals[v*3+1],data.Normals[v*3+2]));
            }
            var input=new GrassCellInput(resolution,cell.Inner,cell.X*10.0,cell.Z*10.0,retireIndex,samples,left,right,wallSide,closest.Position,closest.Normal);
            result=new GrassSurfaceSnapshot(data,receipt,walls,input,closest.Index,bound);return true;
        }
    }
}
