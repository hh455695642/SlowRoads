using System;
using SlowRoads.Generation.Streaming;
using SlowRoads.Generation.Spatial;

namespace SlowRoads.Generation.Terrain
{
    public enum FarTerrainStage { Heights0, Normals0, Heights1, Normals1, Complete, Cancelled }
    /// <summary>Dt geometry, Settings L42865–43040. One row per work unit;
    /// same Pl context throughout all stages, independent of render origin.</summary>
    public sealed class FarTerrainTileBuilder : IDisposable
    {
        readonly RoadStreamingSession owner;
        TerrainBuildContext context;
        readonly int f,z;
        int row;
        bool firstHeight=true;
        public readonly double OriginX,OriginZ;
        double[] heights;
        public TerrainBuffer<double> Heights=>Data?.Heights??throw new InvalidOperationException("Tile is not complete");
        float[] positions,normals,coarseNormals;
        public TerrainBuffer<float> Positions=>Data?.Positions??throw new InvalidOperationException("Tile is not complete");
        public TerrainBuffer<float> Normals=>Data?.Normals??throw new InvalidOperationException("Tile is not complete");
        public TerrainBuffer<float> CoarseNormals=>Data?.CoarseNormals??throw new InvalidOperationException("Tile is not complete");
        bool[] coarseHeightValid,coarseNormalValid;
        public TerrainBuffer<bool> CoarseHeightValid=>Data?.CoarseHeightValid??throw new InvalidOperationException("Tile is not complete");
        public TerrainBuffer<bool> CoarseNormalValid=>Data?.CoarseNormalValid??throw new InvalidOperationException("Tile is not complete");
        public readonly TerrainGridTopology Topology;
        public FarTerrainStage Stage {get;private set;}
        public TerrainMeshData Data {get;private set;}
        public int Generation=>context.Generation;
        public long Revision=>context.Revision;
        public double LookupHeight(double worldX,double worldZ,bool fine=true)=>Data?.LookupHeight(worldX,worldZ,fine)??throw new InvalidOperationException("Tile is not complete");
        public FarTerrainTileBuilder(RoadStreamingSession session,int tileX,int tileZ)
        {
            owner=session??throw new ArgumentNullException(nameof(session));
            f=owner.TerrainConfig.TileFineResolution;z=f+1;
            OriginX=(double)tileX*owner.TerrainConfig.TileSize;OriginZ=(double)tileZ*owner.TerrainConfig.TileSize;
            Topology=new TerrainGridTopology(f);heights=new double[z*z];positions=new float[z*z*3];normals=new float[z*z*3];coarseNormals=new float[z*z*3];
            coarseHeightValid=new bool[z*z];coarseNormalValid=new bool[z*z];
            for(int x=0;x<=f;x++)for(int y=0;y<=f;y++){int at=(x*z+y)*3;positions[at]=(float)((double)x/f*owner.TerrainConfig.TileSize);positions[at+2]=(float)((double)y/f*owner.TerrainConfig.TileSize);}
            var request=new[]{new TerrainSampleRequest(OriginX,OriginZ)};
            if(owner.TryBeginTerrainBuild(request,0,0,null,out context)!=RoadQueryStatus.Ready)throw new InvalidOperationException("Tile requires a completed producer boundary");
            context.Advance(1);
        }
        double Height(double x,double y)
        {
            // No allocation per vertex. Only this bounded builder accesses the session
            // service; external consumers still use the versioned batch interface.
            if(firstHeight){firstHeight=false;return context.GetResult(0).Height;}
            return owner.ReadTerrainRaw(context,x,y);
        }
        void SetHeight(int x,int y,bool coarse)
        {
            int at=x*z+y;double h=Height(OriginX+x*10,OriginZ+y*10);
            heights[at]=h;positions[at*3+1]=(float)h;if(coarse)coarseHeightValid[at]=true;
        }
        void Normal(int x,int y,bool fine)
        {
            bool edgeFine=(x%2!=0&&(y==0||y==f))||(y%2!=0&&(x==0||x==f));
            if(fine&&edgeFine)return;
            double wx=OriginX+x*10,wz=OriginZ+y*10,left,right,up,down,span;
            if(!fine&&edgeFine)
            {left=Height(wx,wz-10);right=Height(wx,wz+10);up=Height(wx-10,wz);down=Height(wx+10,wz);span=20;}
            else
            {
                int step=fine?1:2;double d=step*10;span=d*2;
                left=y==0?Height(wx,wz-d):heights[x*z+y-step];
                right=y==f?Height(wx,wz+d):heights[x*z+y+step];
                up=x==0?Height(wx-d,wz):heights[(x-step)*z+y];
                down=x==f?Height(wx+d,wz):heights[(x+step)*z+y];
            }
            double nx=-(down-up)*span,ny=span*span,nz=-span*(right-left);
            double inv=1/Math.Sqrt(nx*nx+ny*ny+nz*nz);int at=(x*z+y)*3;
            normals[at]=(float)(nx*inv);normals[at+1]=(float)(ny*inv);normals[at+2]=(float)(nz*inv);
            if(!fine){coarseNormalValid[x*z+y]=true;Array.Copy(normals,at,coarseNormals,at,3);}
        }
        public FarTerrainStage Advance(int budget)
        {
            if(budget<0)throw new ArgumentOutOfRangeException(nameof(budget));
            if(Stage>=FarTerrainStage.Complete)return Stage;
            if(!owner.IsTerrainBuildCurrent(context)){Dispose();return Stage;}
            try{while(budget-->0&&Stage<FarTerrainStage.Complete)
            {
                if(Stage==FarTerrainStage.Heights0)
                {
                    for(int y=0;y<=f;y+=(row>0&&row<f)?2:1)SetHeight(row,y,true);
                    row++;if(row<=f){SetHeight(row,0,true);SetHeight(row,f,true);}row++;
                }
                else if(Stage==FarTerrainStage.Heights1)
                {
                    if(row==0)row++;
                    if(row==f)row++;else {for(int y=0;y<=f;y++)SetHeight(row,y,false);row++;}
                }
                else
                {
                    bool fine=Stage==FarTerrainStage.Normals1;
                    for(int y=0;y<=f;y+=fine?1:row%2!=0?f:row>0&&row<f?2:1)Normal(row,y,fine);
                    row++;
                }
                context.ReportProgress();
                if(row>f){row=0;Stage++;}
            }
            if(Stage==FarTerrainStage.Complete)
            {
                if(!context.TryCommit())Stage=FarTerrainStage.Cancelled;
                else {var indices=new int[Topology.CoarseCount+Topology.FineCount];
                    for(int i=0;i<Topology.CoarseCount;i++)indices[i]=Topology.GetCoarse(i);
                    for(int i=0;i<Topology.FineCount;i++)indices[Topology.CoarseCount+i]=Topology.GetFine(i);
                    Data=new TerrainMeshData(owner,context,false,OriginX,OriginZ,positions,normals,coarseNormals,indices,Topology.CoarseCount,Topology.CoarseCount,Topology.FineCount,heights:heights,heightValid:coarseHeightValid,normalValid:coarseNormalValid);
                    ClearBuffers();}
            }
            return Stage;}catch{Dispose();throw;}
        }
        void ClearBuffers(){heights=null;positions=normals=coarseNormals=null;coarseHeightValid=coarseNormalValid=null;}
        public void Dispose(){context?.Dispose();ClearBuffers();if(Stage!=FarTerrainStage.Complete)Stage=FarTerrainStage.Cancelled;}
    }
}
