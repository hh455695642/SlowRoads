using System;

namespace SlowRoads.Generation.Road
{
    /// <summary>Hills Nl, HillsRoad.source.txt L04910–L05145. No shader/terrain queries.
    /// Preserve separate float writebacks, shifted bridge cursor and object-local paint constraints.</summary>
    internal sealed class HillsRoadAttributes
    {
        readonly RoadMeshBuffers b;
        readonly RoadSegmentBuildState state;
        RoadPathSnapshot path;
        RoadAttributeOverlay overlay;
        int bridgeCursor,shadowCursor,paintCursor;
        bool skipRender;
        internal int ShadowCursor=>shadowCursor;
        internal int BridgeCursor=>bridgeCursor;
        internal int PaintCursor=>paintCursor;
        internal HillsRoadAttributes(RoadMeshBuffers buffers,RoadSegmentBuildState state){b=buffers;this.state=state;}
        internal void ForgetInput(){path=null;overlay=null;}
        internal void Begin(RoadPathSnapshot input,RoadAttributeOverlay attributes,int head,int tail)
        {
            path=input;overlay=attributes;bridgeCursor=shadowCursor=paintCursor=0;skipRender=false;
            var point=path.GetFine(path.FineOffset(head));
            Pair(b.Bridge,ref bridgeCursor,path.IsBridge(head)?1:0);
            Shadow(point);
            if(b.Config.OffRoad)
            {
                if(!path.IsBridge(point.NextCoarse)&&!path.IsBridge(point.PreviousCoarse))Lower(1);
                skipRender=true;
            }
            var t=overlay.Get(tail);var next=overlay.Get(tail+1);
            t.MergeLeft=t.MergeRight=false;t.HasMergeLeft=t.HasMergeRight=true;
            // Assigning JS undefined creates the property; retain NaN + presence separately.
            t.Left=next.Left;t.Right=next.Right;t.HasLeft=t.HasRight=true;overlay.Set(tail,t);
        }
        internal void Next(RoadFineSample point)
        {
            if(b.Config.OffRoad)
            {
                if(!path.IsBridge(point.NextCoarse)&&!path.IsBridge(point.PreviousCoarse))
                    Lower(path.IsBridge(point.NextCoarse+1)||path.IsBridge(point.PreviousCoarse-1)?.25:1);
                else skipRender=false;
            }
            Shadow(point);
        }
        void Lower(double amount)
        {
            // Nl reads Float32Array AFTER D wrote its double result; never combine the two operations.
            int p=b.VertexCount*3;
            b.Position[p-2]=(float)((double)b.Position[p-2]-amount);
            b.Position[p-5]=(float)((double)b.Position[p-5]-amount);
        }
        void Shadow(RoadFineSample point)
        {
            Put(b.Shadow,shadowCursor++,point.HasShadowLeft?point.ShadowLeft:0);
            Put(b.Shadow,shadowCursor++,point.HasShadowRight?point.ShadowRight:0);
        }
        static void Put(float[] target,int index,double value){if(index<target.Length)target[index]=(float)value;}
        static void Pair(float[] target,ref int cursor,double value){Put(target,cursor++,value);Put(target,cursor++,value);}
        internal void Fine(int from,int to,double start,double end,double t0,double t1)
        {
            // Both source terms read o.bridge in original Nl; do not turn this into r->o interpolation.
            double bridge=path.IsBridge(to)?1:0;
            Pair(b.Bridge,ref bridgeCursor,bridge*(1-t0)+bridge*t0);
            var a=overlay.Get(from);var c=overlay.Get(to);
            a.HasSolid=c.HasSolid=a.HasSolidLeft=c.HasSolidLeft=a.HasSolidRight=c.HasSolidRight=true;
            a.SolidLeft=a.Left>.5;c.SolidLeft=c.Left>.5;a.SolidRight=a.Right>.5;c.SolidRight=c.Right>.5;
            a.Solid=a.SolidLeft&&a.SolidRight;c.Solid=c.SolidLeft&&c.SolidRight;
            overlay.Set(from,a);overlay.Set(to,c);
            Paint(b.PaintSolid,0,a.Solid,c.Solid,start,end,t0,t1,false);
            Paint(b.PaintSolidLeft,1,a.SolidLeft,c.SolidLeft,start,end,t0,t1,false);
            Paint(b.PaintSolidRight,2,a.SolidRight,c.SolidRight,start,end,t0,t1,false);
            Paint(b.PaintMergeLeft,3,a.MergeLeft,c.MergeLeft,start,end,t0,t1,true);
            Paint(b.PaintMergeRight,4,a.MergeRight,c.MergeRight,start,end,t0,t1,true);
            paintCursor+=2;
        }
        void Paint(float[] target,int channel,bool from,bool to,double start,double end,double t0,double t1,bool merge)
        {
            double? constraint=state.PaintConstraints[channel];double value;
            if(from==to)
            {
                if(constraint.HasValue){value=constraint.Value;state.PaintConstraints[channel]=null;}
                else value=from?1:0;
            }
            else
            {
                double a=Math.Ceiling(start),c=Math.Ceiling(end);
                if(a==c)value=from?1:0;
                else
                {
                    double length=end-start,fraction=(a-start)/length;
                    if(merge&&to&&c-a>=2)fraction+=(c-a-1)/length;
                    state.PaintConstraints[channel]=null;
                    value=constraint??(t0<fraction?(from?1:0):(to?1:0));
                    if(t0<fraction&&t1>fraction)
                    {
                        double ratio=(fraction-t0)/(t1-t0);
                        state.PaintConstraints[channel]=value+(.5-value)/ratio;
                    }
                }
            }
            Put(target,paintCursor,value);Put(target,paintCursor+1,value);
        }
        internal void Finish(int tail,double phase)
        {
            // Extra bridge pair may exceed capacity at fs=10: JS typed-array writes are ignored.
            Fine(tail,tail+1,phase,phase+b.Config.UvStep,0,1);b.Visible=!skipRender;
        }
        internal int RegenerateShadows(int head,int tail)
        {
            shadowCursor=0;
            // node.next.prev.i is owner for an interior sample, node.i for a coarse sample.
            for(int i=path.FineOffset(head);i<path.FineCount;i++)
            {
                var n=path.GetFine(i);if(n.Owner>tail)break;
                Shadow(n);
            }
            return shadowCursor;
        }
    }
}
