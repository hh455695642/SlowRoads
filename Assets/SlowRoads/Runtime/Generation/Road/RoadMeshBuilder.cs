using System;
using SlowRoads.Generation.Midline;

namespace SlowRoads.Generation.Road
{
    /// <summary>Original D/Nl bounded segment builder. P5 supplies explicit requests, input
    /// revisions and phase handoffs. One work unit is one coarse interval (at most ten fine steps).</summary>
    public sealed class RoadMeshBuilder
    {
        public readonly RoadMeshBuffers Buffers;
        public readonly RoadSegmentBuildState State;
        public int BridgeWrites=>Buffers.Config.BaseOnly?0:attributes.BridgeCursor;
        public int ShadowWrites=>Buffers.Config.BaseOnly?0:attributes.ShadowCursor;
        public int PaintWrites=>Buffers.Config.BaseOnly?0:attributes.PaintCursor;
        readonly HillsRoadAttributes attributes;
        RoadPathSnapshot path;
        int head,tail,pointCursor,coarseCursor,fineIndexCursor;
        Double3 coarseMin,coarseMax;
        public RoadMeshBuilder(RoadMeshBuffers buffers,RoadSegmentBuildState state=null)
        {
            Buffers=buffers??throw new ArgumentNullException(nameof(buffers));State=state??new RoadSegmentBuildState();
            attributes=new HillsRoadAttributes(buffers,State);
        }
        public RoadBuildResult Begin(RoadSegmentRequest request,RoadPathSnapshot input,RoadAttributeOverlay overlay)
        {
            if(input==null||overlay==null)throw new ArgumentNullException();
            if(!ReferenceEquals(input,overlay.Path))throw new ArgumentException("Overlay belongs to a different snapshot");
            if(request.ConfigurationId!=Buffers.Config.Id)throw new ArgumentException("Configuration changed; create a matching segment buffer");
            if(request.Generation!=input.Generation||request.Revision!=input.Revision)
                return State.Result=RoadBuildResult.Invalidated;
            long end=(long)request.HeadCoarseIndex+Buffers.Config.Intervals;
            if(end>=int.MaxValue||end>int.MaxValue-2||!Validate(input,request.HeadCoarseIndex,(int)end))
                return State.Result=RoadBuildResult.NeedMoreInput;
            // Validation is transactional: a missing lookahead leaves old live buffers, phase and overlay untouched.
            if(State.Generation.HasValue&&State.Generation.Value!=request.Generation)Array.Clear(State.PaintConstraints,0,5);
            State.Generation=request.Generation;
            path=input;head=request.HeadCoarseIndex;tail=(int)end;
            Buffers.Request=request;Buffers.Complete=false;Buffers.VertexCount=Buffers.CoarseCount=Buffers.FineCount=0;
            Buffers.Visible=false;Buffers.SetLod(RoadLod.Coarse);
            State.CompletedIntervals=0;State.RebuildFromCoarseIndex=int.MaxValue;State.Phase=request.InitialPhase%10;
            pointCursor=path.FineOffset(head);coarseCursor=head;fineIndexCursor=Buffers.FineStart;
            var p=path.GetFine(pointCursor).Position;
            Buffers.Origin=new Double3(p.X,0,p.Z);coarseMin=coarseMax=p;
            WriteSection(pointCursor);WriteUv(0,State.Phase);
            if(!Buffers.Config.BaseOnly)attributes.Begin(path,overlay,head,tail);
            return State.Result=RoadBuildResult.Building;
        }
        bool Validate(RoadPathSnapshot p,int first,int last)
        {
            if(p.GeometryReadyExclusive<=last||p.NormalReadyExclusive<=last||
                (!Buffers.Config.BaseOnly&&p.AttributeReadyExclusive<=last+1))return false;
            if(Buffers.Config.OffRoad&&p.GeometryReadyExclusive<=last+2)return false;
            if(p.Offset(first)<0||p.Offset(last+1)<0)return false;
            if(Buffers.Config.OffRoad&&p.Offset(last+2)<0)return false;
            int start=p.FineOffset(first),end=p.FineOffset(last);
            if(start<0||end<start||end+1>=p.FineCount)return false;
            for(int j=start;j<=end;j++)
            {
                var n=p.GetFine(j);
                int previous=n.IsCoarse?(n.Owner==p.HeadIndex?int.MinValue:n.Owner-1):n.Owner;
                if(n.PreviousCoarse!=previous||n.NextCoarse!=n.Owner+1)return false;
            }
            int anchor=start;
            for(int c=first;c<last;c++)
            {
                int next=p.FineOffset(c+1),count=next-anchor;
                if(count<1||count>10)return false;
                for(int j=anchor;j<next;j++)
                {
                    var n=p.GetFine(j);
                    if(n.Owner!=c||n.IsCoarse!=(j==anchor)||n.NextCoarse!=c+1)return false;
                }
                anchor=next;
            }
            return true;
        }
        public RoadBuildResult Advance(int coarseIntervalBudget)
        {
            if(coarseIntervalBudget<1)throw new ArgumentOutOfRangeException(nameof(coarseIntervalBudget));
            if(State.Result!=RoadBuildResult.Building)return State.Result;
            for(int i=0;i<coarseIntervalBudget&&coarseCursor<tail;i++)Append();
            if(coarseCursor==tail)Finish();
            return State.Result;
        }
        void Append()
        {
            int firstVertex=Buffers.VertexCount-2,endPoint=path.FineOffset(coarseCursor+1),count=endPoint-pointCursor;
            for(int i=1;i<=count;i++)
            {
                int previous=Buffers.VertexCount-2;
                WriteQuad(fineIndexCursor,previous,previous+1,previous+2,previous+3);fineIndexCursor+=6;
                WriteSection(++pointCursor);
                if(!Buffers.Config.BaseOnly)attributes.Next(path.GetFine(pointCursor));
            }
            double increment=Buffers.Config.UvStep/count,start=State.Phase;
            int uvFirst=firstVertex+2;
            for(int i=0;i<count;i++)
            {
                State.Phase+=increment;WriteUv(uvFirst+i*2,State.Phase);
                if(!Buffers.Config.BaseOnly)attributes.Fine(coarseCursor,coarseCursor+1,start,start+Buffers.Config.UvStep,(double)i/count,(double)(i+1)/count);
            }
            int lastVertex=Buffers.VertexCount-2;
            WriteQuad(State.CompletedIntervals*6,firstVertex,firstVertex+1,lastVertex,lastVertex+1);
            Buffers.Intervals[State.CompletedIntervals]=new RoadIntervalSpan(coarseCursor,firstVertex,lastVertex);
            coarseCursor++;State.CompletedIntervals++;
            var p=path.GetCoarseAt(path.Offset(coarseCursor)).Position;
            coarseMin=Min(coarseMin,p);coarseMax=Max(coarseMax,p);
        }
        void WriteSection(int point)
        {
            var n=path.GetFine(point);var next=path.GetFine(point+1);var p=n.Position;var side=n.Side;
            var tangent=new Double3(next.Position.X-p.X,next.Position.Y-p.Y,next.Position.Z-p.Z).Normalize();
            // normalize(next-p) cross (-side.x,0,-side.z); deliberately no second normalize.
            double bx=-side.X,by=0,bz=-side.Z;
            var normal=new Double3(tangent.Y*bz-tangent.Z*by,tangent.Z*bx-tangent.X*bz,tangent.X*by-tangent.Y*bx);
            int v=Buffers.VertexCount;double width=Buffers.Config.HalfWidth;var o=Buffers.Origin;
            WriteVertex(v,p.X-side.X*width-o.X,p.Y+.02,p.Z-side.Z*width-o.Z,normal);
            WriteVertex(v+1,p.X+side.X*width-o.X,p.Y+.02,p.Z+side.Z*width-o.Z,normal);
            Buffers.VertexCount+=2;
        }
        void WriteVertex(int v,double x,double y,double z,Double3 normal)
        {
            int k=v*3;Buffers.Position[k]=(float)x;Buffers.Position[k+1]=(float)y;Buffers.Position[k+2]=(float)z;
            Buffers.Normal[k]=(float)normal.X;Buffers.Normal[k+1]=(float)normal.Y;Buffers.Normal[k+2]=(float)normal.Z;
        }
        void WriteUv(int v,double phase)
        {int k=v*2;Buffers.Uv[k]=0;Buffers.Uv[k+1]=(float)phase;Buffers.Uv[k+2]=1;Buffers.Uv[k+3]=(float)phase;}
        void WriteQuad(int k,int a,int b,int c,int d)
        {var t=Buffers.Indices;t[k]=a;t[k+1]=b;t[k+2]=c;t[k+3]=b;t[k+4]=d;t[k+5]=c;}
        void Finish()
        {
            Buffers.CoarseCount=State.CompletedIntervals*6;Buffers.FineCount=fineIndexCursor-Buffers.FineStart;
            var center=new Double3((coarseMax.X+coarseMin.X)/2-Buffers.Origin.X,
                (coarseMax.Y+coarseMin.Y)/2,(coarseMax.Z+coarseMin.Z)/2-Buffers.Origin.Z);
            var diagonal=new Double3(coarseMax.X-coarseMin.X,coarseMax.Y-coarseMin.Y,coarseMax.Z-coarseMin.Z);
            Buffers.ReferenceSphere=new RoadReferenceSphere(center,diagonal.Length()/2+5);
            Buffers.SetLod(Buffers.Request.ObserverIndex>=head-Buffers.Config.Intervals/2.0?RoadLod.Fine:RoadLod.Coarse);
            Buffers.Visible=true;
            if(!Buffers.Config.BaseOnly)attributes.Finish(tail,State.Phase);
            // Unity's conservative AABB is a separate platform adaptation, based ONLY on live local vertices.
            var min=new Double3(double.PositiveInfinity,double.PositiveInfinity,double.PositiveInfinity);
            var max=new Double3(double.NegativeInfinity,double.NegativeInfinity,double.NegativeInfinity);
            for(int i=0;i<Buffers.VertexCount*3;i+=3)
            {
                var p=new Double3(Buffers.Position[i],Buffers.Position[i+1],Buffers.Position[i+2]);min=Min(min,p);max=Max(max,p);
            }
            Buffers.BoundsMin=min;Buffers.BoundsMax=max;Buffers.Complete=true;State.Result=RoadBuildResult.Complete;
        }
        static Double3 Min(Double3 a,Double3 b)=>new Double3(Math.Min(a.X,b.X),Math.Min(a.Y,b.Y),Math.Min(a.Z,b.Z));
        static Double3 Max(Double3 a,Double3 b)=>new Double3(Math.Max(a.X,b.X),Math.Max(a.Y,b.Y),Math.Max(a.Z,b.Z));

        public RoadBuildResult CheckRevision(RoadPathSnapshot current)
        {
            if(current==null)throw new ArgumentNullException(nameof(current));
            if(path==null)return State.Result;
            if(current.Generation==path.Generation&&current.Revision==path.Revision)return State.Result;
            int dirty=current.Generation==path.Generation?Math.Min(current.GeometryDirtyFrom,current.AttributeDirtyFrom):head;
            State.RebuildFromCoarseIndex=Math.Max(head,Math.Min(tail,dirty==int.MinValue?dirty:dirty-1));
            // Rebuild the bounded segment from its saved phase + overlay/paint BEFORE state.
            // Attributes can affect the previous interval; P5 must invalidate dependent following segments.
            Buffers.Complete=false;return State.Result=RoadBuildResult.Invalidated;
        }
        public int RegenerateShadows()
        {
            if(!Buffers.Complete||Buffers.Config.BaseOnly)throw new InvalidOperationException("A completed Nl segment is required");
            // Original method reaches interior points of the interval AFTER tail. Require that input explicitly.
            if(path.FineOffset(tail+1)<0)throw new InvalidOperationException("Shadow refresh needs tail+1 fine anchor");
            return attributes.RegenerateShadows(head,tail);
        }
        public void Cancel()
        {
            Buffers.Complete=false;State.Result=RoadBuildResult.Invalidated;State.RebuildFromCoarseIndex=head;
            path=null;attributes.ForgetInput(); // release snapshot/overlay references; retain reusable managed buffer capacity.
        }
    }
}
