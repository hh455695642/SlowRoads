using System;
using System.Collections.Generic;
using SlowRoads.Generation.Midline;
using SlowRoads.Generation.Spatial;

namespace SlowRoads.Generation.Contents.Fields
{
    /// <summary>Ka.loadNextField L44130–44528. One original plot per work unit.
    /// All geometry is recorded, then expanded by C3; callbacks retain the original random order.</summary>
    public sealed class FieldClipBuilder
    {
        readonly FieldTemplate template;readonly IReadOnlyList<FieldCandidate> candidates;readonly FieldRandomState random;readonly IFieldWorld world;
        readonly double tileSize,wallDistance,halfWidth,treeFactor;readonly bool colour;readonly int densityOption;readonly int? previousOwner;
        int cursor,treeOrdinal;readonly FieldExecutionState execution;public FieldBoundaryRecipe Recipe {get;}
        public bool Complete=>cursor>=candidates.Count;
        public FieldClipBuilder(FieldTemplate template,IReadOnlyList<FieldCandidate> candidates,FieldRandomState random,IFieldWorld world,
            int tileX,int tileZ,double tileSize,int generation,int owner,int? previousOwner,double halfWidth,int detail,int densityOption,bool colour,FieldExecutionState execution=null)
        {this.template=template;this.candidates=candidates;this.random=random;this.world=world;this.tileSize=tileSize;this.previousOwner=previousOwner;
            this.halfWidth=halfWidth;wallDistance=halfWidth+2;treeFactor=detail>2?.25:detail*.1;this.densityOption=densityOption;this.colour=colour;
            this.execution=execution??new FieldExecutionState();Recipe=new FieldBoundaryRecipe(tileX,tileZ,tileSize,generation,owner);}
        FieldNodeProjection Project(double x,double z,CoarseNode start)
        {
            if(!RoadProjection.TryCreate(x,z,start,out var p))throw new InvalidOperationException("Field projection lacks frozen road support");
            return new FieldNodeProjection{Index=p.Index,Side=p.Side,Distance=p.Distance};
        }
        CoarseNode Node(FieldNodeProjection p)=>world.Node(p.Index)??throw new InvalidOperationException("Field cache outside retained road support");
        static bool Culled(FieldNodeProjection p,double side)=>p.Side!=side||p.Distance<15;
        static void Include(ref CoarseNode first,ref CoarseNode last,CoarseNode node)
        {if(first==null||node.Index<first.Index)first=node;if(last==null||node.Index>last.Index)last=node;}
        static int Outside(double x,double z,FieldTemplate.Plot p)=>x<p.MinX?1:x>p.MaxX?2:z<p.MinZ?3:z>p.MaxZ?4:-1;
        double Density(double h)=>random.TreeDensity(h,densityOption);
        void Event(FieldId field,int edge,FieldBoundaryOperation op,Double3 p,Double3 n,double u,double shadow)
            =>Recipe.events.Add(new FieldBoundaryEvent(field,edge,op,p,n,u,shadow));
        bool Tree(FieldId field,int edge,Double3 p,double scale,int type,double? density=null)
        {
            var tree=new FieldTree(field,edge,treeOrdinal++,p,random.Rotation.Next(),scale,type,(int)random.Variant.Next(),density);
            bool accepted=world.AddTree(tree);if(accepted)Recipe.trees.Add(tree);return accepted;
        }
        public bool Step()
        {
            if(Complete)return true;var candidate=candidates[cursor++];var ft=candidate.Tile;var plot=template.Plots[candidate.Index];var id=candidate.Id;
            double cx=ft.WorldX+plot.CenterX,cz=ft.WorldZ+plot.CenterZ;
            var center=world.ClosestNode(Recipe.TileX,Recipe.TileZ,cx,cz,plot.Radius+wallDistance+10);
            bool near=center!=null||world.CloseToRoad(Recipe.TileX,Recipe.TileZ,cx,cz,plot.Radius+50),overlap=false;double side=0;
            CoarseNode first=null,last=null;
            if(center!=null)
            {
                side=(center.Position.X-cx)*center.Normal.X+(center.Position.Z-cz)*center.Normal.Z<0?-1:1;
                int culled=0,culledIndex=-1;bool invalid=false;
                foreach(int ni in plot.Nodes)
                {
                    var p=ft.Projections[ni];
                    if(p==null)
                    {var n=template.Nodes[ni];var lastLookup=world.ClosestNode(Recipe.TileX,Recipe.TileZ,ft.WorldX+n.X,ft.WorldZ+n.Z,plot.Radius)??center;execution.LastLookupIndex=lastLookup.Index;
                        p=Project(ft.WorldX+n.X,ft.WorldZ+n.Z,lastLookup);ft.Projections[ni]=p;}
                    var node=Node(p);
                    if(Culled(p,side)){culled++;culledIndex=execution.LastLookupIndex??throw new InvalidOperationException("Original projection scratch unavailable");}
                    else Include(ref first,ref last,node);
                    if(side<0&&node.Grade< -2||side>0&&node.Grade>2)invalid=true;
                }
                if(culled==0||invalid)return Complete;
                if(culled==1&&(culledIndex>last.Index+5||culledIndex<first.Index-5))return Complete;
                bool inside=false;int transitions=0,entry=-1,exit=-1;bool entered=false,exited=false;double margin=side*(halfWidth+2);
                for(var n=first;n!=null&&n.Index<=last.Index;)
                {
                    double x=n.Position.X-margin*n.Normal.X-ft.WorldX,z=n.Position.Z-margin*n.Normal.Z-ft.WorldZ;
                    int outside=Outside(x,z,plot);
                    if(outside>0){if(inside){if(!exited){exited=true;exit=outside;}transitions++;}inside=false;}
                    else{if(!inside){if(!entered){entered=true;if(n.Previous?.Previous!=null){var p=n.Previous.Previous.Position;entry=Outside(p.X-ft.WorldX,p.Z-ft.WorldZ,plot);}}transitions++;}inside=true;}
                    n=n.Next;if(n==null)throw new InvalidOperationException("Field scan requires next sentinel");if(n.HeadingDelta<.1&&n.HeadingDelta>-.1)n=n.Next;
                }
                if(transitions>2||entry==exit&&entry>0)return Complete;
            }
            double minX=tileSize,minZ=tileSize,maxX=0,maxZ=0;first=last=null;
            foreach(int edge in plot.Edges)
            {
                int? owner=ft.DrawnWalls[edge];if(owner>=0&&owner!=previousOwner)continue;
                var wall=template.Edges[edge];var a=template.Nodes[wall.A];var b=template.Nodes[wall.B];
                double treesA=ft.Densities[wall.A],treesB=ft.Densities[wall.B],length=wall.Length;
                var pa=new Double3(ft.WorldX+a.X-Recipe.WorldX,ft.Heights[wall.A],ft.WorldZ+a.Z-Recipe.WorldZ);
                var pb=new Double3(ft.WorldX+b.X-Recipe.WorldX,ft.Heights[wall.B],ft.WorldZ+b.Z-Recipe.WorldZ);
                bool intersection=false;int roadside=0;FieldNodeProjection al=null,bl=null;
                if(center!=null)
                {
                    al=ft.Projections[wall.A];bl=ft.Projections[wall.B];
                    if(Culled(al,side))
                    {
                        intersection=true;roadside=0;overlap=true;if(Culled(bl,side))continue;
                        var n=Node(bl);Include(ref first,ref last,n);if(bl.Drawn)continue;
                        double x=n.Position.X-n.Normal.X*(wallDistance+.19)*bl.Side,z=n.Position.Z-n.Normal.Z*(wallDistance+.19)*bl.Side;
                        pa=new Double3(x-Recipe.WorldX,world.RoadsideHeight(x,z,n),z-Recipe.WorldZ);
                        length=Math.Sqrt((pb.X-pa.X)*(pb.X-pa.X)+(pb.Z-pa.Z)*(pb.Z-pa.Z));bl.Drawn=true;
                        Event(id,edge,FieldBoundaryOperation.Pillar,pa,n.Normal,0,world.Density(x,z));
                    }
                    else if(Culled(bl,side))
                    {
                        intersection=true;roadside=1;overlap=true;if(Culled(al,side))continue;
                        var n=Node(al);Include(ref first,ref last,n);if(al.Drawn)continue;
                        double x=n.Position.X-n.Normal.X*(wallDistance+.19)*al.Side,z=n.Position.Z-n.Normal.Z*(wallDistance+.19)*al.Side;
                        pb=new Double3(x-Recipe.WorldX,world.RoadsideHeight(x,z,n),z-Recipe.WorldZ);
                        length=Math.Sqrt((pb.X-pa.X)*(pb.X-pa.X)+(pb.Z-pa.Z)*(pb.Z-pa.Z));al.Drawn=true;
                        Event(id,edge,FieldBoundaryOperation.Pillar,pb,n.Normal,0,world.Density(x,z));
                    }
                }
                var normal=new Double3(pa.Z-pb.Z,0,pb.X-pa.X).Normalize();double shadow=intersection?world.Density(pa.X+Recipe.WorldX,pa.Z+Recipe.WorldZ):0;
                Event(id,edge,FieldBoundaryOperation.Start,pa,normal,0,shadow);
                int points=(int)Math.Max(0,Math.Floor((length-(near?intersection?1:4:10))/(near?intersection?2:8:20)))+1;
                int previousPoint=0;double previousY=pa.Y,perPoint=length/points;shadow=0;
                for(int pi=1;pi<points;pi++)
                {
                    double t=(double)pi/points,inv=1-t,pll;
                    if(intersection&&length>15){pll=(roadside==0?t:inv)*length;if(pll>30){if(pi%4!=0)continue;}else if(pll>15&&pi%2!=0)continue;}else pll=100;
                    double x=pa.X*inv+pb.X*t,z=pa.Z*inv+pb.Z*t,y;
                    shadow=intersection?world.Density(x+Recipe.WorldX,z+Recipe.WorldZ):0;
                    if(intersection&&(length<=20||pll<=20))
                    {double wx=x+Recipe.WorldX,wz=z+Recipe.WorldZ;var na=Node(al);var nb=Node(bl);
                        double da=(wx-na.Position.X)*(wx-na.Position.X)+(wz-na.Position.Z)*(wz-na.Position.Z),db=(wx-nb.Position.X)*(wx-nb.Position.X)+(wz-nb.Position.Z)*(wz-nb.Position.Z);
                        y=world.RoadsideHeight(wx,wz,da<db?na:nb);}
                    else y=world.RawHeight(x+Recipe.WorldX,z+Recipe.WorldZ);
                    double distance=(pi-previousPoint)*perPoint,td=inv*treesA+t*treesB,scaleFactor=1+td/2;int count=(int)Math.Floor(treeFactor*td*distance+.5);
                    for(int j=0;j<count;j++)
                    {
                        if(random.Placement.Next()>td||intersection&&pll<2)continue;double fraction=(double)j/count,scale=random.Scale.Next()*scaleFactor,sink=3*scale;
                        if(intersection&&pll<30)sink*=.25+pll/45;int type=random.TreeType();if(type==1)sink/=2;
                        double offset=1+random.Placement.Next();
                        Tree(id,edge,new Double3(x+Recipe.WorldX+normal.X*offset-normal.Z*fraction*distance,previousY*fraction+y*(1-fraction)-sink,
                            z+Recipe.WorldZ+normal.Z*offset+normal.X*fraction*distance),scale,type);
                    }
                    previousPoint=pi;previousY=y;Event(id,edge,FieldBoundaryOperation.Point,new Double3(x,y,z),normal,length/5*t,shadow);
                }
                Event(id,edge,FieldBoundaryOperation.End,pb,normal,length/5,shadow);if(!intersection)ft.DrawnWalls[edge]=Recipe.Owner;
                minX=Math.Min(minX,Math.Min(pa.X,pb.X));maxX=Math.Max(maxX,Math.Max(pa.X,pb.X));minZ=Math.Min(minZ,Math.Min(pa.Z,pb.Z));maxZ=Math.Max(maxZ,Math.Max(pa.Z,pb.Z));
            }
            if(overlap&&first!=null&&last!=null)
            {
                double da=Density(first.Position.Y),db=Density(last.Position.Y);int difference=last.Index-first.Index;
                for(var n=first;difference<100&&n.Index<last.Index;n=n.Next)
                {
                    bool left=side>0;if(world.Sidewall(n.Index,left)!=0)continue;world.WriteSidewall(id,n.Index,left,left?-wallDistance:wallDistance);
                    if(n.Index<=first.Index)continue;double t=(double)(n.Index-first.Index)/difference,td=db*t+da*(1-t),scaleFactor=1+td/2;
                    int count=(int)Math.Floor(treeFactor*td*8+.5),accepted=0;MidlinePathNode treeNode=n;
                    for(int j=0;j<count;j++)
                    {
                        if(j>0&&n.HasFineSegments&&n.FineSegments>2)
                        {
                            if(treeNode.NextFine==null)throw new InvalidOperationException("Field trees lack fine support");if(ReferenceEquals(treeNode.NextFine,n.Next))break;
                            treeNode=treeNode.NextFine;
                            if(n.FineSegments>3&&!ReferenceEquals(treeNode.NextFine,n.Next)){treeNode=treeNode.NextFine;
                                if(n.FineSegments>4&&!ReferenceEquals(treeNode.NextFine,n.Next))treeNode=treeNode.NextFine;}
                        }
                        if(random.Placement.Next()>td)continue;double fraction=(double)j/count,scale=random.Scale.Next()*scaleFactor;if(random.Shared.Next()<.04)scale*=1.25;
                        int type=random.TreeType();if(type==1&&scale>1.5)scale=1.5;double sink=scale+random.Placement.Next();Double3 p;
                        if(!n.HasFineSegments||n.FineSegments<3)p=new Double3(n.Position.X-n.Normal.X*side*(wallDistance+2)+n.Normal.Z*fraction*10,
                            n.Position.Y*(1-fraction)+n.Next.Position.Y*fraction-sink,n.Position.Z-n.Normal.Z*side*(wallDistance+2)-n.Normal.X*fraction*10);
                        else p=new Double3(treeNode.Position.X-n.Normal.X*side*(wallDistance+2),treeNode.Position.Y-sink,treeNode.Position.Z-n.Normal.Z*side*(wallDistance+2));
                        if(Tree(id,-1,p,scale,type,td/2))accepted++;
                    }
                    if(accepted>0)world.WriteShadow(id,n,side,Math.Min(1,accepted/2.0));
                }
            }
            bool outsideBoundary=minX<0||maxX>tileSize||minZ<0||maxZ>tileSize;
            if(!overlap&&!outsideBoundary&&colour)
            {double col=random.Colour.Next();for(double x=minX+1;x<maxX-1;x+=10)for(double z=minZ+1;z<maxZ-1;z+=10)
                    if(x>=0&&z>=0&&x<=tileSize&&z<=tileSize)world.WriteColour(id,x+Recipe.WorldX,z+Recipe.WorldZ,col);}
            return Complete;
        }
    }
}
