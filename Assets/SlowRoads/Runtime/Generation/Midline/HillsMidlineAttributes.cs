using System;

namespace SlowRoads.Generation.Midline
{
    /// <summary>B7.extendFine shadow writes, Settings L47137–L47170.
    /// One operation per budget unit. Provider belongs to the caller; no vegetation or rendering.
    /// Markings are a separate successor phase, never interleaved with these writes.</summary>
    public sealed class HillsMidlineAttributes
    {
        readonly Func<double,double,double> query;
        readonly double halfWidth;
        CoarseNode node;
        MidlinePathNode interpolation;
        int phase=4,ordinal,count;
        public bool IsComplete => phase==4;
        internal void Clear(){node=null;interpolation=null;phase=4;}
        public int DirtyFromCoarseIndex { get; private set; }
        public HillsMidlineAttributes(Func<double,double,double> query,double halfWidth)
        {
            this.query=query??throw new ArgumentNullException(nameof(query));
            if(!(halfWidth>0))throw new ArgumentOutOfRangeException(nameof(halfWidth));
            this.halfWidth=halfWidth;
        }
        public void Begin(CoarseNode completed)
        {
            if(!IsComplete)throw new InvalidOperationException("Finish the previous shadow interval.");
            node=completed??throw new ArgumentNullException(nameof(completed));
            DirtyFromCoarseIndex=node.Previous?.Index??node.Index;
            phase=0;
        }
        public bool Advance(int budget)
        {
            if(budget<1)throw new ArgumentOutOfRangeException(nameof(budget));
            while(budget-- >0 && !IsComplete)
            {
                if(phase==0)
                {
                    double v=query(node.Position.X-node.Normal.X*halfWidth,node.Position.Z-node.Normal.Z*halfWidth);
                    node.ShadowLeft=Truthy(node.HasShadowLeft,node.ShadowLeft)?Math.Max(node.ShadowLeft,v):v;
                    node.HasShadowLeft=true;phase=1;
                }
                else if(phase==1)
                {
                    double v=query(node.Position.X+node.Normal.X*halfWidth,node.Position.Z+node.Normal.Z*halfWidth);
                    node.ShadowRight=Truthy(node.HasShadowRight,node.ShadowRight)?Math.Max(node.ShadowRight,v):v;
                    node.HasShadowRight=true;phase=2;
                }
                else if(phase==2)
                {
                    double th=node.Position.Y-node.HeightCorrection;
                    if(th<5)
                    {
                        double left=1,right=1;
                        if(node.Grade<0)left=Math.Max(0,1-node.Grade/-2);
                        else right=Math.Max(0,1-node.Grade/2);
                        node.ShadowLeft*=Math.Min(th/5,left);
                        node.ShadowRight*=Math.Min(th/5,right);
                    }
                    // Source's high-altitude branch reads M.g (never assigned), not M.n.g.
                    // undefined compares false; do not replace it with the node's Grade.
                    if(node.Bridge){node.ShadowLeft*=0;node.ShadowRight*=0;}
                    if(node.Previous!=null && node.Previous.HasFineSegments && node.Previous.FineSegments>0)
                    {
                        count=node.Previous.FineSegments+1;ordinal=1;
                        interpolation=node.Previous.NextFine;phase=3;
                    }
                    else phase=4;
                }
                else
                {
                    double t=ordinal/(double)count;
                    // Missing source endpoint values produce NaN, not an implicit zero.
                    interpolation.ShadowLeft=node.ShadowLeft*t+
                        (node.Previous.HasShadowLeft?node.Previous.ShadowLeft:double.NaN)*(1-t);
                    interpolation.ShadowRight=node.ShadowRight*t+
                        (node.Previous.HasShadowRight?node.Previous.ShadowRight:double.NaN)*(1-t);
                    interpolation.HasShadowLeft=interpolation.HasShadowRight=true;
                    interpolation=interpolation.NextFine;
                    if(++ordinal>=count)phase=4;
                }
            }
            return IsComplete;
        }
        static bool Truthy(bool has,double value)=>has && value!=0 && !double.IsNaN(value);
    }
}
