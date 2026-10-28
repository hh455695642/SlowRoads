using System;

namespace SlowRoads.Generation.Midline
{
    /// <summary>B7.updateMarkings, Settings L47174–L47277. Mutable state belongs
    /// to one generation session. Long prepend/merge walks yield per node.</summary>
    public sealed class MidlineMarkingsState
    {
        readonly MidlineConfig config;
        public CoarseNode Node { get; private set; }
        public CoarseNode LeftStart { get; private set; }
        public CoarseNode LeftEnd { get; private set; }
        public CoarseNode RightStart { get; private set; }
        public CoarseNode RightEnd { get; private set; }
        public double Da,AbsDa,GradLat,GradLonDiff,Trees,PreviousGradLonDiff,PreviousDa;
        public bool HasLeft,HasRight;
        public double LeftExtensions,RightExtensions;
        CoarseNode current,walk;
        double leftThreshold,rightThreshold,turn,prepend;
        int phase=5;
        public bool IsComplete=>phase==5;
        internal void Clear(){Node=current=walk=LeftStart=LeftEnd=RightStart=RightEnd=null;phase=5;}
        public int DirtyFromCoarseIndex { get; private set; }
        public MidlineMarkingsState(MidlineConfig config,CoarseNode head)
        { this.config=config??throw new ArgumentNullException(nameof(config));Node=head??throw new ArgumentNullException(nameof(head)); }
        public void Begin(CoarseNode completed)
        {
            if(!IsComplete)throw new InvalidOperationException("Complete the current markings operation.");
            current=completed??throw new ArgumentNullException(nameof(completed));phase=0;
            DirtyFromCoarseIndex=Node.Index;
        }
        public bool Advance(int budget)
        {
            if(budget<1)throw new ArgumentOutOfRangeException(nameof(budget));
            while(budget-- >0 && !IsComplete)
            {
                switch(phase)
                {
                    case 0: Prepare();break;
                    case 1: Left();break;
                    case 2: Right();break;
                    case 3: Finish();break;
                    case 10:
                        if(LeftEnd.Index<Node.Index){LeftEnd=LeftEnd.Next;PaintLeft(LeftEnd);}
                        else {LeftEnd=Node;phase=2;}
                        break;
                    case 11:
                        if(walk!=null && walk.Index>Node.Index-prepend)
                        {
                            PaintLeft(walk);
                            // Source compares J.i with a plain node object (NaN), not its i.
                            walk=walk.Previous;
                        }
                        else
                        {
                            walk=walk?.Previous?.Previous?.Previous;
                            if(walk!=null){walk.PaintMergeLeft=true;walk.HasPaintMergeLeft=true;Dirty(walk);}
                            LeftEnd=Node;phase=2;
                        }
                        break;
                    case 20:
                        if(RightEnd.Index<Node.Index){RightEnd=RightEnd.Next;PaintRight(RightEnd);}
                        else {RightEnd=Node;phase=3;}
                        break;
                    default: throw new InvalidOperationException("Invalid markings phase.");
                }
            }
            return IsComplete;
        }
        static double ShadowMinimum(CoarseNode n)=>Math.Min(n.HasShadowLeft?n.ShadowLeft:double.NaN,
            n.HasShadowRight?n.ShadowRight:double.NaN);
        void Prepare()
        {
            Da+=current.HeadingDelta;AbsDa+=Math.Abs(current.HeadingDelta);
            GradLat+=current.Grade;Trees+=ShadowMinimum(current);
            double margin=config.MarkingsMargin;
            if(Node.Index>current.Index-margin+1){phase=5;return;}
            double threshold=config.SolidLineThreshold,n=Trees/margin,d=GradLat/margin;
            if(Math.Abs(d)<0.5 || Math.Sign(d)!=Math.Sign(Da))d=0;
            else {d=Math.Abs(d);d=d<0.5?0:Math.Min(1,(d-0.5)*2);}
            if(Math.Sign(current.GradeFiltered)==Math.Sign(Node.GradeFiltered))GradLonDiff=0;
            else
            {
                GradLonDiff=current.GradeFiltered-Node.GradeFiltered;
                double y=Math.Abs(current.GradeFiltered)/(Math.Abs(current.GradeFiltered)+Math.Abs(Node.GradeFiltered));
                y=Math.Abs(y-0.5)*2;GradLonDiff*=1-y;
            }
            double u=GradLonDiff*0.3+0.7*PreviousGradLonDiff;PreviousGradLonDiff=u;
            double f=u/config.SolidLineCrestThreshold,a=Math.Max(n,Math.Max(d,f));
            a*=1+Math.Min(1,f);threshold*=1-a*0.5;
            leftThreshold=threshold*(1-(Da/margin)*4*a);
            rightThreshold=threshold*(1+(Da/margin)*4*a);
            turn=Math.Abs(Da/margin)*0.75+0.25*PreviousDa;PreviousDa=turn;
            phase=1;
        }
        void Left()
        {
            Node.PaintSolidLeftWeight=0;Node.HasPaintSolidLeftWeight=true;Dirty(Node);
            if(turn>leftThreshold)
            {
                PaintLeft(Node);LeftExtensions=config.MarkingsMargin;LeftExtensions-=2;
                if(!HasLeft)
                {
                    HasLeft=true;prepend=config.MarkingsMargin/2;
                    if(leftThreshold==0)prepend+=2;
                    if(LeftEnd!=null && Node.Index-LeftEnd.Index<9+prepend){phase=10;return;}
                    LeftStart=Node;walk=Node.Previous;phase=11;return;
                }
            }
            else if(HasLeft)
            {
                PaintLeft(Node);LeftEnd=Node;
                double y=leftThreshold*0.6;
                if(turn<y)
                {
                    LeftExtensions--;if(turn<y*0.5)LeftExtensions--;
                    if(LeftExtensions<=0)HasLeft=false;
                }
            }
            phase=2;
        }
        void Right()
        {
            Node.PaintSolidRightWeight=0;Node.HasPaintSolidRightWeight=true;Dirty(Node);
            if(turn>rightThreshold)
            {
                PaintRight(Node);RightExtensions=config.MarkingsMargin+1;
                if(!HasRight)
                {
                    HasRight=true;
                    if(RightEnd!=null && Node.Index-RightEnd.Index<9){phase=20;return;}
                    if(RightEnd!=null)
                    {
                        var y=RightEnd.Next?.Next?.Next;
                        if(y!=null){y.PaintMergeRight=true;y.HasPaintMergeRight=true;Dirty(y);}
                    }
                    RightStart=Node;
                    RightEnd=Node;
                }
            }
            else if(HasRight)
            {
                PaintRight(Node);RightEnd=Node;double y=rightThreshold*0.6;
                if(turn<y){RightExtensions--;if(RightExtensions<=0)HasRight=false;}
            }
            phase=3;
        }
        void Finish()
        {
            Da-=Node.HeadingDelta;AbsDa-=Math.Abs(Node.HeadingDelta);
            GradLat-=Node.Grade;Trees-=ShadowMinimum(Node);
            Node=Node.Next;phase=5;
        }
        void PaintLeft(CoarseNode n){n.PaintSolidLeftWeight=1;n.HasPaintSolidLeftWeight=true;Dirty(n);}
        void PaintRight(CoarseNode n){n.PaintSolidRightWeight=1;n.HasPaintSolidRightWeight=true;Dirty(n);}
        void Dirty(CoarseNode n)=>DirtyFromCoarseIndex=Math.Min(DirtyFromCoarseIndex,n.Index);
    }
}
