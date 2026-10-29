using System;
using System.Collections.Generic;
using SlowRoads.Generation.Midline;

namespace SlowRoads.Generation.Spatial
{
    public enum RoadQueryStatus { Ready, NoRoad, NotResident }
    // Ca: Settings L41836–L42013. Values are owned by this index, never pooled midline objects.
    public sealed class RoadSpatialIndex
    {
        public sealed class Segment
        {
            public int Head,Tail;public int? Mid;
            public double MinX,MaxX,MinZ,MaxZ;
        }
        sealed class Tile { public int LastSeen;public readonly List<Segment> Segments=new(); }
        readonly Dictionary<(int,int),Tile> tiles=new();
        readonly Dictionary<int,Double3> positions=new();
        readonly List<(int,int)> retire=new();readonly List<int> prune=new();
        readonly Func<int,Double3?> source;
        public readonly int TileSize,Margin,Horizon;
        readonly int stride;
        int headFloor;
        public int LookaheadIndex {get;private set;}
        public int TileCount=>tiles.Count;
        public int CachedNodes=>positions.Count;
        public RoadSpatialIndex(int tileSize,int margin,Func<int,Double3?> source,int head)
        {
            if(tileSize<1||margin<1)throw new ArgumentOutOfRangeException();
            TileSize=tileSize;Margin=margin;this.source=source;Horizon=Math.Max(500,Math.Min(1900,tileSize*3/2));
            stride=margin*2;if(stride>tileSize)stride/=2;LookaheadIndex=head;headFloor=head;
        }
        static int TileCoordinate(double v,int size)
        {double x=Math.Floor(v/size);if(x<int.MinValue||x>int.MaxValue)throw new OverflowException("Spatial tile domain exhausted");return (int)x;}
        Double3 P(int i)
        {
            if(positions.TryGetValue(i,out var p))return p;
            var value=source(i);if(!value.HasValue)throw new InvalidOperationException("Missing Ca query neighbour "+i);
            return positions[i]=value.Value;
        }
        bool Has(int i)=>positions.ContainsKey(i)||source(i).HasValue;
        public bool Advance(int head)
        {
            // Se severs prev each time a node becomes head. Detached JS nodes kept
            // by Ca do NOT retain a backwards chain; value copies must preserve that.
            headFloor=head;
            if(LookaheadIndex>=head+Horizon)return false;
            var point=P(LookaheadIndex);int? previousX=null,previousZ=null;
            for(int x=-Margin;x<=Margin;x+=stride)for(int z=-Margin;z<=Margin;z+=stride)
            {
                int tx=TileCoordinate(point.X+x,TileSize),tz=TileCoordinate(point.Z+z,TileSize);
                if(tx==previousX&&tz==previousZ)continue;previousX=tx;previousZ=tz;
                if(!tiles.TryGetValue((tx,tz),out var tile))tiles[(tx,tz)]=tile=new Tile();
                tile.LastSeen=LookaheadIndex;var seg=tile.Segments.Count==0?null:tile.Segments[0];
                if(seg==null||seg.Tail<LookaheadIndex-2||seg.Tail-seg.Head>=20)
                {
                    if(seg!=null&&seg.Tail-seg.Head>15)seg.Mid=(int)Math.Ceiling((seg.Head+(double)seg.Tail)/2);
                    tile.Segments.Insert(0,new Segment{Head=LookaheadIndex,Tail=LookaheadIndex,MinX=point.X,MaxX=point.X,MinZ=point.Z,MaxZ=point.Z});
                }
                else if(seg.Tail!=LookaheadIndex)
                {seg.Tail=LookaheadIndex;seg.MinX=Math.Min(point.X,seg.MinX);seg.MaxX=Math.Max(point.X,seg.MaxX);seg.MinZ=Math.Min(point.Z,seg.MinZ);seg.MaxZ=Math.Max(point.Z,seg.MaxZ);}
            }
            P(LookaheadIndex+1);LookaheadIndex++;return true;
        }
        public IReadOnlyList<Segment> GetSegmentsForTile(int x,int z)=>tiles.TryGetValue((x,z),out var t)?t.Segments:null;
        public int? GetLastSeenIndexForTile(int x,int z)=>tiles.TryGetValue((x,z),out var t)?t.LastSeen:(int?)null;
        public void RetireTile(int x,int z)=>tiles.Remove((x,z));
        // Standalone road residency. Terrain later owns explicit RetireTile calls instead.
        public void RetireBefore(int index)
        {
            retire.Clear();int first=LookaheadIndex;
            foreach(var pair in tiles)if(pair.Value.LastSeen<index)retire.Add(pair.Key);else foreach(var seg in pair.Value.Segments)first=Math.Min(first,seg.Head);
            foreach(var key in retire)tiles.Remove(key);
            prune.Clear();foreach(var pair in positions)if(pair.Key<first-2)prune.Add(pair.Key);
            foreach(int key in prune)positions.Remove(key);
        }
        static (double t,double d) Projection(double x,double z,Double3 a,Double3 b)
        {
            double dx=b.X-a.X,dz=b.Z-a.Z,l=dx*dx+dz*dz;
            if(l==0)return (0,(b.X-x)*(b.X-x)+(b.Z-z)*(b.Z-z));
            double t=((x-a.X)*dx+(z-a.Z)*dz)/l;t=Math.Max(0,Math.Min(1,t));
            double px=a.X+t*dx,pz=a.Z+t*dz;return (t,(px-x)*(px-x)+(pz-z)*(pz-z));
        }
        static bool Outside(Segment s,double x,double z,double r)=>x<s.MinX-r||x>s.MaxX+r||z<s.MinZ-r||z>s.MaxZ+r;
        double Distance(int i,double x,double z){var p=P(i);return (x-p.X)*(x-p.X)+(z-p.Z)*(z-p.Z);}
        public RoadQueryStatus GetClosestNode(double x,double z,out int node,out double distanceSquared,double radius=50)
        {
            node=0;distanceSquared=0;if(!tiles.TryGetValue((TileCoordinate(x,TileSize),TileCoordinate(z,TileSize)),out var tile))return RoadQueryStatus.NotResident;
            double closest=radius,sq=radius*radius;int? selected=null;
            foreach(var s in tile.Segments)
            {
                if(Outside(s,x,z,closest))continue;var p=Projection(x,z,P(s.Head),P(s.Tail));bool a=false,b=false;
                if(s.Mid.HasValue&&p.d<sq*2)
                {
                    var pa=Projection(x,z,P(s.Head),P(s.Mid.Value));var pb=Projection(x,z,P(s.Mid.Value),P(s.Tail));
                    if(pa.d<pb.d){if(pa.d<p.d){p=pa;a=true;}}else if(pb.d<p.d){p=pb;b=true;}
                }
                if(p.d<sq*1.25){sq=p.d;closest=Math.Sqrt(sq);selected=p.t<.5?(b?s.Mid:s.Head):(a?s.Mid:s.Tail);}
            }
            if(!selected.HasValue)return RoadQueryStatus.NoRoad;
            int at=selected.Value;double d=Distance(at,x,z);
            while(Has(at+1)&&Distance(at+1,x,z)<d){at++;d=Distance(at,x,z);}
            if(at==selected.Value&&at>headFloor&&Has(at-1))while(at-1>headFloor&&Has(at-2)&&Distance(at-1,x,z)<d){at--;d=Distance(at,x,z);}
            if(d>radius*radius)return RoadQueryStatus.NoRoad;node=at;distanceSquared=d;return RoadQueryStatus.Ready;
        }
        public bool IsCloseToRoad(int tx,int tz,double x,double z,double radius=50)
        {
            if(!tiles.TryGetValue((tx,tz),out var tile))return false;double sq=radius*radius;
            foreach(var s in tile.Segments)
            {
                if(Outside(s,x,z,radius))continue;var p=Projection(x,z,P(s.Head),P(s.Tail));
                if(s.Mid.HasValue&&p.d<sq*1.5)
                {var a=Projection(x,z,P(s.Head),P(s.Mid.Value));var b=Projection(x,z,P(s.Mid.Value),P(s.Tail));if(a.d<b.d){if(a.d<p.d)p=a;}else if(b.d<p.d)p=b;}
                if(p.d<sq)return true;
            }return false;
        }
    }
}
