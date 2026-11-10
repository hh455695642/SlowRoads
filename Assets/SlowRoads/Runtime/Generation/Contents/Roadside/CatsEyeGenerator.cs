using System;
using System.Collections.Generic;
using SlowRoads.Generation.Midline;

namespace SlowRoads.Generation.Contents.Roadside
{
    public readonly struct CatsEyePoint
    {
        public readonly int CoarseIndex,Ordinal,Side;public readonly Double3 Position,Normal;
        internal CatsEyePoint(int index,int ordinal,int side,Double3 position,Double3 normal)
        {CoarseIndex=index;Ordinal=ordinal;Side=side;Position=position;Normal=normal;}
    }
    /// <summary>x7.drawNextNode S L45372–45411. Phase uses nominal ten-unit coarse intervals,
    /// never actual arc length. Rendering/light visibility does not change this logical stream.</summary>
    public sealed class CatsEyeGenerator
    {
        readonly double spacing,sideOffset;readonly List<CatsEyePoint> points=new();CoarseNode node;double phase;
        public IReadOnlyList<CatsEyePoint> Points=>points;public int NextIndex=>node.Index;public double Phase=>phase;
        public event Action<CatsEyePoint,CoarseNode> Emitted;
        public event Action<int> NodeCompleted;
        public void DiscardHistory()=>points.Clear();
        public CatsEyeGenerator(CoarseNode start,double spacing,double sideOffset)
        {
            if(start==null||!double.IsFinite(spacing+sideOffset)||spacing<=0||spacing<.01)throw new ArgumentOutOfRangeException();
            node=start;this.spacing=spacing;this.sideOffset=sideOffset;
        }
        public int Advance(int targetExclusive,int budget)
        {
            if(budget<0)throw new ArgumentOutOfRangeException(nameof(budget));int used=0;
            while(used<budget&&node.Index<targetExclusive)
            {
                if(node.Next==null||node.Next.NextFine==null)break;Draw();NodeCompleted?.Invoke(node.Index);node=node.Next;used++;
            }
            return used;
        }
        void Draw()
        {
            int ordinal=0;
            // The source performs one final phase increment even when incoming phase >=10.
            while(true)
            {
                if(phase>=0&&phase<10)
                {
                    int fineIndex=Math.Max(0,(int)Math.Floor(phase/10*node.FineSegments));double fineDistance=10.0/node.FineSegments;
                    MidlinePathNode a=node;for(int i=0;i<fineIndex;i++)a=a.NextFine??throw new InvalidOperationException("Catseye fine support missing");
                    var b=a.NextFine??throw new InvalidOperationException("Catseye next fine support missing");double t=(phase-fineIndex*fineDistance)/fineDistance;
                    // Preserve weighted-add order, distinct from Vector3.Lerp.
                    double x=a.Position.X*(1-t)+b.Position.X*t,y=a.Position.Y*(1-t)+b.Position.Y*t,z=a.Position.Z*(1-t)+b.Position.Z*t;
                    var n=a.Normal;var normal=new Double3(-n.Z,0,n.X);
                    var left=new CatsEyePoint(node.Index,ordinal,1,new Double3(x+n.X*sideOffset,y+.03,z+n.Z*sideOffset),normal);
                    var right=new CatsEyePoint(node.Index,ordinal,-1,new Double3(x-n.X*sideOffset,y+.03,z-n.Z*sideOffset),normal);
                    points.Add(left);Emitted?.Invoke(left,node);points.Add(right);Emitted?.Invoke(right,node);ordinal++;
                }
                phase+=spacing;if(phase<10)continue;phase-=10;return;
            }
        }
    }
}
