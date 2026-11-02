using System;
using System.Collections.Generic;
namespace SlowRoads.Generation.Contents.Trees
{
    /// <summary>Original i7.customGenNormals0, Settings L45034–45100.
    /// Called in Dt's coarse-normal order, before Float32 normal conversion.</summary>
    public sealed class TreeTileCandidates
    {
        readonly List<TreeCandidate> candidates=new();
        readonly Dictionary<(int,int),int> arrangements=new();
        public IReadOnlyList<TreeCandidate> Candidates {get;}
        public readonly int TileX,TileZ,Event,Size,Generation;
        public long Revision {get;internal set;}
        public int RawCount {get;private set;}
        public int ArrangementFor(int x,int z)=>arrangements[(x,z)];
        readonly TreeCandidateGenerator generator;
        readonly int detail;
        public TreeTileCandidates(int tx,int tz,int ev,int size,int generation,TreeCandidateGenerator generator,int detail)
        {TileX=tx;TileZ=tz;Event=ev;Size=size;Generation=generation;this.generator=generator;this.detail=detail;Candidates=candidates.AsReadOnly();}
        public void SampleNormal(int x,int z,double normalY,Func<double,double,double> height)
        {
            int f=Size/10;
            if(normalY<.7||x==f||z==f||(x&1)!=0||(z&1)!=0)return;
            double wx=(double)TileX*Size+x*10,wz=(double)TileZ*Size+z*10;
            // X-major/Z-minor and four calls per coarse vertex are significant RNG order.
            for(int ix=0;ix<2;ix++)for(int iz=0;iz<2;iz++)
            {
                double gx=wx+ix*10,gz=wz+iz*10;var group=generator.Generate(gx,gz);
                if(group.Count>0)arrangements[(x+ix,z+iz)]=group.Arrangement;
                double scale=group.Type*.5+1.08+(group.Count==3?.08:0);
                double density=detail<3?group.Count/3.0:group.NearDensity;
                for(int slot=0;slot<group.Count;slot++)
                {
                    double px=gx+TreeArrangementTable.X(group.Arrangement,slot),pz=gz+TreeArrangementTable.Z(group.Arrangement,slot);
                    double y=height(px,pz)-2-(detail<3?1-density:(1-density)*3);
                    bool valid=y>2;int index=valid?RawCount++:-1;
                    int bucket=valid&&group.Count>1&&generator.Random.NextBucket()>1/(group.Count*.8)?1:0;
                    candidates.Add(new(new(TileX,TileZ,Event,x+ix,z+iz,slot),px,y,pz,
                        TreeArrangementTable.Rotation(group.Arrangement,slot),TreeArrangementTable.Scale(group.Arrangement,slot)*scale,
                        group.Type,group.Variant(slot),Math.Sqrt(density),index,bucket,valid,scale));
                }
            }
        }
    }
}
