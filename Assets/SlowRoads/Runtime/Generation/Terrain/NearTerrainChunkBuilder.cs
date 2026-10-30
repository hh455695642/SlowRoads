using System;
using System.Collections.Generic;
using SlowRoads.Generation.Spatial;
using SlowRoads.Generation.Streaming;

namespace SlowRoads.Generation.Terrain
{
    public enum NearTerrainStage { Positions, CoarseNormals, FineNormals, Complete, Cancelled }

    /// <summary>U/s0 geometry, Settings L41593–41754 and L46177–46214.
    /// One row per work unit. The private road snapshot remains retained across all cells.
    /// Contents (tree density and wall elevation) are outside this geometry stage.</summary>
    public sealed class NearTerrainChunkBuilder : IDisposable
    {
        readonly RoadStreamingSession owner;
        readonly NearTerrainChunkLayout layout;
        readonly TerrainBuildContext context;
        readonly TerrainGridTopology topology;
        readonly int resolution, side, cellVertices;
        readonly double step, originX, originZ, roadWidth;
        double[,] corners;
        float[] positions, normals, coarseNormals, proximity, uv;
        int[] indices;
        readonly int coarseCount, fineStart, fineCount;
        TerrainMeshData[] parents;
        int cellIndex, row, selectedCell=-1;
        public NearTerrainStage Stage {get;private set;}
        public TerrainMeshData Data {get;private set;}

        /// <summary>U.parentTilesHaveStaged: caller supplies completed T2 parents.
        /// T4 decides when to schedule; this constructor never generates missing parents.</summary>
        public NearTerrainChunkBuilder(RoadStreamingSession session, NearTerrainChunkLayout layout, int supportFirst, int supportLast,
            IReadOnlyList<TerrainMeshData> parentTiles)
            :this(ValidateParents(session,layout,parentTiles),layout,supportFirst,supportLast)
        {
            parents=new TerrainMeshData[parentTiles.Count];for(int i=0;i<parents.Length;i++)parents[i]=parentTiles[i];
        }
        static RoadStreamingSession ValidateParents(RoadStreamingSession session,NearTerrainChunkLayout layout,IReadOnlyList<TerrainMeshData> tiles)
        {
            if(session==null||layout==null||tiles==null)throw new ArgumentNullException();
            foreach(var key in layout.ParentTiles)
            {
                bool found=false;
                foreach(var tile in tiles)if(tile!=null&&!tile.RoadDependent&&tile.CanPublish(session)&&
                    tile.Config.TileSize==session.TerrainConfig.TileSize&&tile.OriginX==(double)key.x*tile.Config.TileSize&&tile.OriginZ==(double)key.z*tile.Config.TileSize){found=true;break;}
                if(!found)throw new InvalidOperationException("Required parent tile is not staged");
            }
            return session;
        }
        // Independent geometry fixtures provide an explicit parent-free controlled context.
        // Production callers must use the public parent-validated constructor above.
        internal NearTerrainChunkBuilder(RoadStreamingSession session, NearTerrainChunkLayout layout, int supportFirst, int supportLast)
        {
            owner=session??throw new ArgumentNullException(nameof(session));
            this.layout=layout??throw new ArgumentNullException(nameof(layout));
            if(layout.Config.Identity!=session.TerrainConfig.Identity)throw new ArgumentException("Layout configuration differs from session");
            if(!layout.Sealed||layout.Count==0)throw new ArgumentException("Seal cell collection before building");
            resolution=session.TerrainConfig.CellResolution;side=resolution+1;cellVertices=side*side;
            step=session.TerrainConfig.CellStep;roadWidth=session.Config.Mesh.HalfWidth;topology=new TerrainGridTopology(resolution);
            originX=double.MaxValue;originZ=double.MaxValue;int inner=0;
            foreach(var c in layout.Cells){originX=Math.Min(originX,c.X*10.0);originZ=Math.Min(originZ,c.Z*10.0);if(c.Inner)inner++;}
            int vertices=cellVertices*layout.Count;
            positions=new float[vertices*3];normals=new float[vertices*3];coarseNormals=new float[vertices*3];
            proximity=new float[vertices];uv=new float[vertices*2];corners=new double[layout.Count,4];
            coarseCount=layout.Count*topology.CoarseCount;fineStart=inner*topology.CoarseCount;
            fineCount=coarseCount-fineStart+inner*topology.FineCount;indices=new int[fineStart+fineCount];
            var first=layout.Cells[0];
            if(owner.TryBeginTerrainBuild(new[]{new TerrainSampleRequest(first.X*10.0,first.Z*10.0)},
                supportFirst,supportLast,first.Candidates(),out context,true)!=RoadQueryStatus.Ready)
                throw new InvalidOperationException("Chunk road support is not ready");
            try
            {
                context.Advance(1);
                // Original initGeneration visits all TL corners before the other corners.
                for(int i=0;i<layout.Count;i++)corners[i,0]=i==0?context.GetResult(0).Height:owner.ReadTerrainRaw(context,layout.Cells[i].X*10.0,layout.Cells[i].Z*10.0);
                int ci=0,co=fineStart,fi=coarseCount;
                for(int i=0;i<layout.Count;i++)
                {
                    var c=layout.Cells[i];int start=c.Inner?ci:co;
                    for(int k=0;k<topology.CoarseCount;k++)indices[start+k]=i*cellVertices+topology.GetCoarse(k);
                    if(c.Inner){ci+=topology.CoarseCount;for(int k=0;k<topology.FineCount;k++)indices[fi+k]=i*cellVertices+topology.GetFine(k);fi+=topology.FineCount;}
                    else co+=topology.CoarseCount;
                }
            }
            catch{context.Dispose();throw;}
        }
        void SelectCell()
        {
            if(selectedCell==cellIndex)return;
            selectedCell=cellIndex;var c=layout.Cells[cellIndex];context.Window.SetCandidates(c.Candidates());
            if(Stage!=NearTerrainStage.Positions)return;
            corners[cellIndex,1]=owner.ReadTerrainRaw(context,c.X*10.0+10,c.Z*10.0);
            corners[cellIndex,2]=owner.ReadTerrainRaw(context,c.X*10.0,c.Z*10.0+10);
            corners[cellIndex,3]=owner.ReadTerrainRaw(context,c.X*10.0+10,c.Z*10.0+10);
        }
        TerrainHeightResult Height(double x,double z,bool bilinear,bool fine)
        {
            var c=layout.Cells[cellIndex];
            var input=new TerrainHeightInput(c.X*10.0+x*step,c.Z*10.0+z*step,x,z,resolution,
                corners[cellIndex,0],corners[cellIndex,1],corners[cellIndex,2],corners[cellIndex,3],bilinear,
                fine?TerrainProjectionMode.Fine:TerrainProjectionMode.Coarse);
            if(owner.EvaluateTerrain(context,input,out var value)!=RoadQueryStatus.Ready)
                throw new InvalidOperationException("Chunk projection exceeds retained road support");
            return value;
        }
        void PositionRow()
        {
            var c=layout.Cells[cellIndex];
            for(int z=0;z<=resolution;z++)
            {
                var value=Height(row,z,true,true);var p=value.Projection;int at=cellIndex*cellVertices+row*side+z;
                positions[at*3]=(float)(c.X*10.0-originX+row*step);positions[at*3+1]=(float)value.Height;
                positions[at*3+2]=(float)(c.Z*10.0-originZ+z*step);
                proximity[at]=(float)(p.Distance-roadWidth);
                if(p.Distance<roadWidth&&value.Height-p.Y<-.2)proximity[at]=(float)Math.Max(-.001,p.OriginalDistance-roadWidth);
                // Settings imports Hills.N as l1: roadUv is populated only for off-road styles.
                if(owner.Config.Mesh.OffRoad&&p.OriginalDistance<roadWidth*2){uv[at*2]=(float)(.5-(p.OriginalDistance/(roadWidth*2)*p.Side)/2);uv[at*2+1]=(float)(p.Index+p.T);}
            }
        }
        void Normal(int x,int z,bool fine)
        {
            int at=cellIndex*cellVertices+x*side+z;
            // Original final coarse row queries one extra edge row. Typed-array writes
            // beyond the live buffer are ignored; preserve query order and next-cell writes.
            double prox=at<proximity.Length?proximity[at]:0;
            double stride=fine?1:prox>12?(prox>24?resolution:1+(prox-12)/24*(resolution-1)):1;
            double up=Height(x,z-stride,false,fine).Height,down=Height(x,z+stride,false,fine).Height;
            double left=Height(x-stride,z,false,fine).Height,right=Height(x+stride,z,false,fine).Height;
            double span=step*stride*2,nx=-(right-left)*span,ny=span*span,nz=-span*(down-up);
            double inv=1/Math.Sqrt(nx*nx+ny*ny+nz*nz);
            if(at>=proximity.Length)return;
            normals[at*3]=(float)(nx*inv);normals[at*3+1]=(float)(ny*inv);normals[at*3+2]=(float)(nz*inv);
        }
        public NearTerrainStage Advance(int budget)
        {
            if(budget<0)throw new ArgumentOutOfRangeException(nameof(budget));
            if(Stage>=NearTerrainStage.Complete)return Stage;
            if(!owner.IsTerrainBuildCurrent(context)){Dispose();return Stage;}
            if(parents!=null)foreach(var parent in parents)if(!parent.CanPublish(owner)){Dispose();return Stage;}
            try
            {
                while(budget-->0&&Stage<NearTerrainStage.Complete)
                {
                    if(Stage==NearTerrainStage.FineNormals&&!layout.Cells[cellIndex].Inner){NextCell();continue;}
                    SelectCell();
                    if(Stage==NearTerrainStage.Positions){PositionRow();row++;}
                    else if(Stage==NearTerrainStage.CoarseNormals)
                    {
                        for(int z=0;z<=resolution;z+=row>0&&row<resolution?2:1)Normal(row,z,false);
                        row++;Normal(row,0,false);Normal(row,resolution,false);row++;
                    }
                    else {for(int z=0;z<=resolution;z++)Normal(row,z,true);row++;}
                    context.ReportProgress();
                    if(row>resolution)NextCell();
                }
                if(Stage==NearTerrainStage.Complete)
                {
                    if(!context.TryCommit()){Stage=NearTerrainStage.Cancelled;return Stage;}
                    Data=new TerrainMeshData(owner,context,true,originX,originZ,positions,normals,coarseNormals,indices,coarseCount,fineStart,fineCount,proximity,uv,layout);
                    ClearBuffers();
                }
                return Stage;
            }
            catch{Dispose();throw;}
        }
        void NextCell()
        {
            row=0;selectedCell=-1;
            if(++cellIndex<layout.Count)return;
            cellIndex=0;if(Stage==NearTerrainStage.CoarseNormals)Array.Copy(normals,coarseNormals,normals.Length);
            Stage++;
        }
        void ClearBuffers(){positions=normals=coarseNormals=proximity=uv=null;indices=null;corners=null;parents=null;}
        public void Dispose(){context.Dispose();ClearBuffers();if(Stage!=NearTerrainStage.Complete)Stage=NearTerrainStage.Cancelled;}
    }
}
