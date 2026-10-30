using System;
using System.Collections.Generic;
using SlowRoads.Generation.Midline;
using SlowRoads.Generation.Spatial;
using SlowRoads.Generation.Streaming;

namespace SlowRoads.Generation.Terrain
{
    public enum TerrainBuildState { Building, Ready, Committed, Cancelled, NotResident }
    /// <summary>World coordinates; CellX/Z identify a 10m cell, including normal halo queries.
    /// Raw requests use Pl only. Mixed requests reuse context-owned corner values.</summary>
    public readonly struct TerrainSampleRequest
    {
        public readonly double X,Z;
        public readonly int CellX,CellZ;
        public readonly bool Raw,Bilinear;
        public readonly TerrainProjectionMode Projection;
        public TerrainSampleRequest(double x,double z,int cellX=0,int cellZ=0,bool raw=true,bool bilinear=true,
            TerrainProjectionMode projection=TerrainProjectionMode.Fine)
        {
            if(!double.IsFinite(x)||!double.IsFinite(z)||(uint)projection>1||cellX==int.MaxValue||cellZ==int.MaxValue)throw new ArgumentOutOfRangeException();
            // U normal stride is at most one 10m cell. Reject an accidental unbounded halo.
            if(!raw&&(x<cellX*10.0-10||x>cellX*10.0+20||z<cellZ*10.0-10||z>cellZ*10.0+20))
                throw new ArgumentOutOfRangeException("Sample exceeds the cell's one-cell normal halo");
            X=x;Z=z;CellX=cellX;CellZ=cellZ;Raw=raw;Bilinear=bilinear;Projection=projection;
        }
    }

    /// <summary>Finite sample batch for T2/T3. Owns corners/results and a private road graph;
    /// holds the producer at a completed phase boundary until commit/cancel. No Mesh ownership.
    /// Performance: <=4096 requests, <=256 coarse nodes, <=128 ordered candidates; split larger work.
    /// All calls are on the session thread. A context never advances the producer itself.</summary>
    public sealed class TerrainBuildContext : IDisposable
    {
        public const int MaxSamples=4096,MaxTotalSamples=262144,MaxProducerWaits=8192;
        readonly RoadStreamingSession owner;
        TerrainSampleRequest[] requests;
        TerrainHeightResult[] results;
        readonly Dictionary<(int x,int z),double> corners=new();
        internal readonly TerrainQueryWindow Window;
        public readonly TerrainGenerationConfig Config;
        public readonly int Generation,NegativeXLayer,NonNegativeXLayer;
        public readonly long Revision,LayerRevision;
        public TerrainBuildState State {get;private set;}=TerrainBuildState.Building;
        public int CompletedSamples {get;private set;}
        internal long MeasuredRawTicks,MeasuredMixedTicks;
        internal int MeasuredRawCalls,MeasuredMixedCalls;
        public int PlannedSamples {get;private set;}
        public int SampleCount=>requests.Length;
        public int FirstSupportIndex=>Window?.FirstIndex??int.MinValue;
        public int LastSupportIndex=>Window?.LastIndex??int.MinValue;
        int producerWaits;
        internal TerrainBuildContext(RoadStreamingSession owner,TerrainSampleRequest[] input,TerrainQueryWindow window,
            int negative,int positive,long layerRevision)
        {
            this.owner=owner;Window=window;Config=owner.TerrainConfig;
            Generation=owner.Midline.State.Generation;Revision=owner.Midline.Revision;
            NegativeXLayer=negative;NonNegativeXLayer=positive;LayerRevision=layerRevision;
            requests=(TerrainSampleRequest[])input.Clone();results=new TerrainHeightResult[input.Length];
            PlannedSamples=input.Length;
        }
        internal bool ProducerWait()
        {
            if(!owner.IsTerrainBuildCurrent(this)){Cancel();return false;}
            if(++producerWaits<=MaxProducerWaits)return true;
            Cancel();return false; // Abandoned/zero-budget work cannot freeze the road indefinitely.
        }
        // Mesh rows also make progress while bypassing the request batch.
        internal void ReportProgress(){producerWaits=0;}
        public TerrainBuildState Advance(int budget)
        {
            if(budget<0)throw new ArgumentOutOfRangeException(nameof(budget));
            if(State!=TerrainBuildState.Building)return State;
            if(!owner.IsTerrainBuildCurrent(this)){Cancel();return State;}
            try
            {
                for(int used=0;used<budget&&CompletedSamples<requests.Length;used++)
                {
                    var q=requests[CompletedSamples];TerrainHeightResult value;
                    if(q.Raw)
                    {double h=owner.ReadTerrainRaw(this,q.X,q.Z);value=new TerrainHeightResult(h,h,default,default,default);}
                    else
                    {
                        double Corner(int x,int z)
                        {
                            if(corners.TryGetValue((x,z),out double h))return h;
                            h=owner.ReadTerrainRaw(this,x*10.0,z*10.0);corners.Add((x,z),h);return h;
                        }
                        var input=new TerrainHeightInput(q.X,q.Z,(q.X-q.CellX*10.0)/Config.CellStep,(q.Z-q.CellZ*10.0)/Config.CellStep,
                            Config.CellResolution,Corner(q.CellX,q.CellZ),Corner(q.CellX+1,q.CellZ),Corner(q.CellX,q.CellZ+1),Corner(q.CellX+1,q.CellZ+1),q.Bilinear,q.Projection);
                        if(owner.EvaluateTerrain(this,input,out value)!=RoadQueryStatus.Ready)
                        {Release(TerrainBuildState.NotResident);return State;}
                    }
                    results[CompletedSamples++]=value;
                    producerWaits=0;
                }
                if(CompletedSamples==requests.Length)State=TerrainBuildState.Ready;
                return State;
            }
            catch {Cancel();throw;}
        }
        public TerrainHeightResult GetResult(int index)
        {
            if(results==null||index<0||index>=CompletedSamples)throw new InvalidOperationException("Result is unavailable");
            return results[index];
        }
        /// <summary>T3 can drain one cell/row into its own mesh buffer and reuse this
        /// same token for the next batch. The road never resumes between batches.
        /// This is not a second snapshot or a published partial chunk.</summary>
        public bool Continue(TerrainSampleRequest[] next,int[] orderedCandidates=null)
        {
            if(State!=TerrainBuildState.Ready||!owner.IsTerrainBuildCurrent(this)){Cancel();return false;}
            if(next==null||next.Length==0||next.Length>MaxSamples||PlannedSamples+next.Length>MaxTotalSamples)
                throw new ArgumentOutOfRangeException(nameof(next));
            foreach(var q in next)if(!q.Raw&&Window==null)throw new InvalidOperationException("Raw context has no road support");
            if(orderedCandidates!=null)
            {
                if(Window==null)throw new InvalidOperationException("Raw context has no road support");
                Window.SetCandidates(orderedCandidates);
            }
            PlannedSamples+=next.Length;requests=(TerrainSampleRequest[])next.Clone();
            if(results.Length!=next.Length)results=new TerrainHeightResult[next.Length];
            CompletedSamples=0;corners.Clear();State=TerrainBuildState.Building;return true;
        }
        public bool TryGetFineAttributes(MidlineSampleKey key,out MidlineFineSample current,out MidlineFineSample next)
        {
            current=next=default;
            return owner.IsTerrainBuildCurrent(this)&&Window!=null&&Window.TryGetFineAttributes(key,out current,out next);
        }
        /// <summary>Call at the actual publication point, before any further producer Advance.
        /// The committed batch carries its generation/revision for T4 dependency tracking.</summary>
        public bool TryCommit()
        {
            if(State!=TerrainBuildState.Ready||!owner.IsTerrainBuildCurrent(this)){Cancel();return false;}
            State=TerrainBuildState.Committed;Window?.Dispose();corners.Clear();owner.ReleaseTerrainBuild(this);return true;
        }
        void Release(TerrainBuildState state)
        {State=state;Window?.Dispose();corners.Clear();results=null;owner.ReleaseTerrainBuild(this);}
        public void Cancel(){if(State!=TerrainBuildState.Committed)Release(TerrainBuildState.Cancelled);}
        public void Dispose(){if(State==TerrainBuildState.Committed){results=null;corners.Clear();}else Cancel();}
    }
}
