using System;
using System.Collections.Generic;
using SlowRoads.Generation.Midline;
using SlowRoads.Generation.Road;
using SlowRoads.Generation.Spatial;
using SlowRoads.Generation.Terrain;

namespace SlowRoads.Generation.Streaming
{
    public sealed class RoadStreamingConfig
    {
        public readonly int Intervals,TileSize,Margin,FineHorizon,RetainedBehindNodes,ViewDistance,TerrainDetail;
        public readonly RoadMeshConfig Mesh;
        public RoadStreamingConfig(int detail=2,int retainedBehindNodes=100,HillsLaneStyle laneStyle=HillsLaneStyle.Standard,int terrainDetail=2)
        {
            if(detail<0||detail>5)throw new ArgumentOutOfRangeException(nameof(detail));
            if((uint)terrainDetail>4)throw new ArgumentOutOfRangeException(nameof(terrainDetail));
            ViewDistance=detail;TerrainDetail=terrainDetail;
            if(retainedBehindNodes<2)throw new ArgumentOutOfRangeException(nameof(retainedBehindNodes));RetainedBehindNodes=retainedBehindNodes;
            Intervals=new[]{25,25,30,35,42,50}[detail];TileSize=new[]{240,480,840,1500,2000,2500}[detail];Margin=new[]{140,160,180,200,200,200}[detail];
            FineHorizon=new[]{200,300,400,500,600,600}[detail];
            if((int)laneStyle<0||(int)laneStyle>4)throw new ArgumentOutOfRangeException(nameof(laneStyle),"Hills supports the five Slow-mode road types");
            Mesh=new RoadMeshConfig(laneStyle,Intervals);
        }
    }
    public sealed class StreamingRoadSlot
    {
        public readonly int Id;
        public readonly RoadMeshBuilder Builder;
        public RoadPathSnapshot Input {get;internal set;}
        internal RoadAttributeOverlay Overlay;
        internal RoadPaintState BeforePaint;
        internal double InitialPhase;
        internal int Head,RebuildIntervals;
        internal RoadPaintNode? PendingBoundary;
        public int PublishedHead {get;internal set;}=int.MaxValue;
        public int PublishedTail {get;internal set;}=int.MinValue;
        public bool Published=>PublishedTail>PublishedHead;
        internal StreamingRoadSlot(int id,RoadMeshConfig config){Id=id;Builder=new RoadMeshBuilder(new RoadMeshBuffers(config));}
    }
    /// <summary>Ni -> Se/B7 -> Ca -> xi, without terrain/content. Settings L50409.
    /// CPU sequential work has explicit units. GPU submissions belong to the presentation owner.</summary>
    public sealed class RoadStreamingSession : IDisposable
    {
        public readonly MidlineSession Midline;
        public readonly MidlineDistanceIndex Distance=new();
        public RoadSpatialIndex Spatial {get;private set;}
        public readonly StreamingRoadSlot[] Slots;
        public readonly RoadStreamingConfig Config;
        public readonly TerrainGenerationConfig TerrainConfig;
        TerrainBuildContext terrainBuild;
        public bool TerrainBuildPending=>terrainBuild!=null;
        public event Action<StreamingRoadSlot> Published;
        public event Action<StreamingRoadSlot,RoadLod> LodChanged;
        public event Action Invalidated;
        public int PreviousSlot {get;private set;}=0;
        public int CurrentSlot {get;private set;}=1;
        public int NextSlot {get;private set;}=2;
        public int LookaheadIndex {get;private set;}
        public double NextSwapIndex {get;private set;}
        public int Tick {get;private set;}
        public int PublishCount {get;private set;}
        public bool Ready=>stage>=3;
        public int Stage=>stage;
        public int MaxPublishedTail {get;private set;}=int.MinValue;
        public int MaxWindowCoarseCount {get;private set;}
        public int RepairCount {get;private set;}
        public int WaitingTicks {get;private set;}
        public bool YieldAtTickBoundary {get;set;}
        int stage,phase,lastFine=int.MinValue,generation=-1,requestedObserver=int.MinValue,retiredAt;
        double roadPhase;
        bool disposed;
        HillsRoadBlend roadBlend;
        readonly object roadBlendOwner=new();
        readonly HillsMidlineHeightSource terrainHeightSource;
        readonly Func<double,double,double> terrainHeight;
        readonly List<StreamingRoadSlot> repairs=new(3);
        int repairIndex;
        public RoadStreamingSession(string seed,RoadStreamingConfig config=null)
        {
            Config=config??new RoadStreamingConfig();var c=MidlineConfig.ForHills(MidlineConfig.HillsStyle.Hills).WithOneDir(true).WithFineHorizon(Config.FineHorizon).WithRoadHalfWidth(Config.Mesh.HalfWidth);
            TerrainConfig=new TerrainGenerationConfig(seed,Config.ViewDistance,Config.TerrainDetail,Config.Mesh.LaneStyle);
            terrainHeightSource=new HillsMidlineHeightSource(c.CreateHeightfieldConfig(seed));
            terrainHeight=(x,z)=>terrainHeightSource.GetXZ(x,z);
            Midline=new MidlineSession(c,seed,terrainHeightSource,ownsHeight:true,recordObserverInputs:false,resetMarkingsOnRestart:true);
            Midline.Begin(seed,0);var mesh=Config.Mesh;
            Slots=new[]{new StreamingRoadSlot(0,mesh),new StreamingRoadSlot(1,mesh),new StreamingRoadSlot(2,mesh)};
        }
        public void SetObserverIndex(int index){requestedObserver=index;}
        void SyncDistance()
        {
            var s=Midline.State;
            if(s.Generation!=generation)
            {
                if(generation>=0){Invalidated?.Invoke();foreach(var slot in Slots){slot.Builder.Cancel();
                    // Native xi recreates D buffers on an epoch reset. Reuse the three
                    // Unity buffers, but reset unused indices to the same constructor state.
                    Array.Clear(slot.Builder.Buffers.Indices,0,slot.Builder.Buffers.Indices.Length);
                    slot.Input=null;slot.Overlay=null;slot.BeforePaint=null;slot.PendingBoundary=null;slot.PublishedHead=int.MaxValue;slot.PublishedTail=int.MinValue;}repairs.Clear();stage=0;phase=0;Spatial=null;}
                generation=s.Generation;Distance.Clear();lastFine=int.MinValue;MaxPublishedTail=int.MinValue;requestedObserver=int.MinValue;
                retiredAt=s.ObserverIndex;
            }
            if(s.FineTail.Index==lastFine)return;
            int first=Distance.Count==0?s.Head.Index:Math.Max(s.Head.Index,Distance.LastCoarseIndex-1);
            var view=Midline.CaptureWindow(first,s.FineTail.Index+1);
            var result=Distance.Update(view);if(result==MidlineDistanceResult.Invalidated)throw new InvalidOperationException("Committed route geometry changed");
            lastFine=s.FineTail.Index;
        }
        void BeginSlot(StreamingRoadSlot slot,int head,double initialPhase,RoadPaintNode? boundary=null)
        {
            slot.Head=head;slot.InitialPhase=initialPhase;slot.PendingBoundary=boundary;slot.Input=null;slot.Overlay=null;
            slot.Builder.Cancel();slot.Builder.State.CompletedIntervals=0;slot.Builder.State.Result=RoadBuildResult.NeedMoreInput;
            TryBeginSlot(slot);
        }
        bool TryBeginSlot(StreamingRoadSlot slot)
        {
            int head=slot.Head;double initialPhase=slot.InitialPhase;var boundary=slot.PendingBoundary;
            var s=Midline.State;int first=Math.Max(s.Head.Index,head-2),last=head+Config.Intervals+3;
            // Off-road Nl reads bridge neighbours two coarse nodes beyond the segment.
            int geometryEnd=head+Config.Intervals+(Config.Mesh.OffRoad?2:0);
            if(s.FineTail.Index<=geometryEnd||Midline.AttributeReadyExclusive<=head+Config.Intervals+1)return false;
            var input=RoadPathSnapshot.FromMidline(Midline.CaptureWindow(first,last));
            if(first>head)throw new InvalidOperationException("Segment start expired before publication");
            MaxWindowCoarseCount=Math.Max(MaxWindowCoarseCount,input.CoarseCount);
            slot.Head=head;slot.InitialPhase=initialPhase;slot.Input=input;slot.Overlay=new RoadAttributeOverlay(input);
            if(boundary.HasValue)slot.Overlay.Set(head,boundary.Value);
            slot.BeforePaint=slot.Builder.State.CapturePaint();
            var request=new RoadSegmentRequest(slot.Id,s.Generation,Midline.Revision,head,slot.Builder.Buffers.Config.Id,initialPhase,s.ObserverIndex);
            if(slot.Builder.Begin(request,input,slot.Overlay)!=RoadBuildResult.Building)throw new InvalidOperationException($"Invalid road window tick={Tick} head={head} geom={input.GeometryReadyExclusive} attrs={input.AttributeReadyExclusive}");
            roadPhase=slot.Builder.State.Phase;
            return true;
        }
        void InitRoad()
        {
            PreviousSlot=0;CurrentSlot=1;NextSlot=2;LookaheadIndex=Midline.State.Head.Index;
            NextSwapIndex=LookaheadIndex+Config.Intervals/2.0;roadPhase=0;BeginSlot(Slots[CurrentSlot],LookaheadIndex,roadPhase);
        }
        void StepRoad()
        {
            var state=Midline.State;var current=Slots[CurrentSlot];
            if(current.Input==null&&!TryBeginSlot(current)){WaitingTicks++;ApplyLod();return;}
            if(LookaheadIndex<state.ObserverIndex+Config.Intervals*2&&Midline.FindCoarse(LookaheadIndex+1)!=null)LookaheadIndex++;
            if(LookaheadIndex>current.Head+current.Builder.State.CompletedIntervals)
            {
                if(!current.Builder.ValidateProducer(generation,Midline.Revision,Midline.GeometryDirtyFrom,Midline.AttributeDirtyFrom,state.ObserverIndex))
                    throw new InvalidOperationException("Road transaction skipped a producer change");
                if(current.Builder.Advance(1)==RoadBuildResult.Complete)
                {
                    roadPhase=current.Builder.State.Phase;current.PublishedHead=current.Head;current.PublishedTail=current.Head+Config.Intervals;
                    MaxPublishedTail=Math.Max(MaxPublishedTail,current.PublishedTail);PublishCount++;Published?.Invoke(current);
                    var boundary=current.Overlay.Get(LookaheadIndex);
                    int temp=NextSlot;NextSlot=PreviousSlot;PreviousSlot=CurrentSlot;CurrentSlot=temp;
                    BeginSlot(Slots[CurrentSlot],LookaheadIndex,roadPhase,boundary);
                }
            }
            ApplyLod();
        }
        void ApplyLod()
        {
            if(Midline.State.ObserverIndex>=NextSwapIndex)
            {
                Slots[PreviousSlot].Builder.Buffers.SetLod(RoadLod.Fine);LodChanged?.Invoke(Slots[PreviousSlot],RoadLod.Fine);
                Slots[CurrentSlot].Builder.Buffers.SetLod(RoadLod.Coarse);LodChanged?.Invoke(Slots[CurrentSlot],RoadLod.Coarse);NextSwapIndex+=Config.Intervals;
            }
        }
        // Freeze producer across all repair work. An immutable P4 window is rebuilt only
        // when an ACTUAL dirty range intersects it; unrelated revision changes are certified above.
        void PrepareRepairs()
        {
            repairs.Clear();int dirty=Math.Min(Midline.GeometryDirtyFrom,Midline.AttributeDirtyFrom);
            foreach(var slot in Slots)
            {
                if(slot.Input==null)continue;
                if(dirty<=slot.Head+Config.Intervals+2 && slot.Head>=Midline.State.Head.Index)
                {slot.RebuildIntervals=slot.Builder.State.CompletedIntervals;repairs.Add(slot);}
                else if(slot.Builder.State.Result==RoadBuildResult.Building)
                {
                    if(!slot.Builder.ValidateProducer(generation,Midline.Revision,Midline.GeometryDirtyFrom,Midline.AttributeDirtyFrom,Midline.State.ObserverIndex))
                        throw new InvalidOperationException("Invalid active road input");
                }
            }
            repairs.Sort((a,b)=>a.Head.CompareTo(b.Head));repairIndex=0;
            if(repairs.Count>0)StartRepair();
        }
        void StartRepair()
        {
            var slot=repairs[repairIndex];var before=slot.BeforePaint;slot.Builder.Cancel();slot.Builder.State.RestorePaint(before);
            double initial=repairIndex==0?slot.InitialPhase:repairs[repairIndex-1].Builder.State.Phase;
            // The oldest retained segment can still depend on its retired predecessor's
            // boundary overlay. Keep that value when there is no predecessor to replay.
            RoadPaintNode? boundary=repairIndex==0?slot.PendingBoundary:repairs[repairIndex-1].Overlay.Get(slot.Head);
            BeginSlot(slot,slot.Head,initial,boundary);RepairCount++;
            if(slot.Input==null)slot.RebuildIntervals=0; // Let producer refill; never freeze it waiting on its own data.
        }
        public int Advance(int budget)
        {
            if(disposed)throw new ObjectDisposedException(nameof(RoadStreamingSession));if(budget<1)throw new ArgumentOutOfRangeException(nameof(budget));
            if(terrainBuild!=null&&terrainBuild.ProducerWait())return 0;
            int used=0;
            while(used++<budget)
            {
                if(stage==0)
                {
                    if(Midline.Advance(1)==MidlineAdvanceResult.Yielded)continue;
                    SyncDistance();var s=Midline.State;
                    if(s.Tail.Index>=s.ObserverIndex+2500&&s.FineTail.Index>=s.ObserverIndex+Config.FineHorizon)
                    {Spatial=new RoadSpatialIndex(Config.TileSize,Config.Margin,i=>Midline.FindCoarse(i)?.Position,s.Head.Index);stage=1;}
                }
                else if(stage==1){if(!Spatial.Advance(Midline.State.Head.Index)){InitRoad();stage=2;}}
                else if(stage==2){StepRoad();if(LookaheadIndex>=Midline.State.ObserverIndex+Config.Intervals){stage=3;phase=0;return used;}}
                else if(phase==0)
                {
                    if(Midline.AtUpdateBoundary&&requestedObserver!=int.MinValue)
                    {Midline.SetObserverIndex(requestedObserver);requestedObserver=int.MinValue;}
                    var result=Midline.Advance(1);if(result==MidlineAdvanceResult.Yielded)continue;
                    SyncDistance();if(stage==0)continue;
                    if(result==MidlineAdvanceResult.CoarseAdded)Spatial.Advance(Midline.State.Head.Index);
                    PrepareRepairs();phase=repairs.Count>0?1:2;
                }
                else if(phase==1)
                {
                    var slot=repairs[repairIndex];
                    if(slot.Builder.State.CompletedIntervals<slot.RebuildIntervals)slot.Builder.Advance(1);
                    if(slot.Builder.State.CompletedIntervals>=slot.RebuildIntervals)
                    {
                        if(slot.Builder.Buffers.Complete){slot.PublishedHead=slot.Head;slot.PublishedTail=slot.Head+Config.Intervals;Published?.Invoke(slot);}
                        if(++repairIndex<repairs.Count)StartRepair();else phase=2;
                    }
                }
                else
                {
                    StepRoad();Tick++;phase=0;
                    if(Midline.State.ObserverIndex>=retiredAt+32){retiredAt=Midline.State.ObserverIndex;Spatial.RetireBefore(retiredAt-Config.RetainedBehindNodes);}
                    if(YieldAtTickBoundary)return used; // test/replay boundary, not a wall-clock budget.
                }
            }
            return budget;
        }
        public bool AtTickBoundary=>Ready&&phase==0&&Midline.AtUpdateBoundary;
        // One bounded phase barrier, shared by raw far-field and mixed near-field jobs.
        // Caller supplies frozen U candidate order after cell registration is complete.
        public RoadQueryStatus TryBeginTerrainBuild(TerrainSampleRequest[] requests,int first,int last,int[] candidates,out TerrainBuildContext context,bool retainRoadSupport=false)
        {
            context=null;
            if(requests==null||requests.Length==0||requests.Length>TerrainBuildContext.MaxSamples)throw new ArgumentOutOfRangeException(nameof(requests));
            if(disposed||terrainBuild!=null||!AtTickBoundary||Midline.State.Generation!=generation)return RoadQueryStatus.NotResident;
            bool mixed=retainRoadSupport;foreach(var request in requests)if(!request.Raw)mixed=true;
            TerrainQueryWindow window=null;
            if(mixed)
            {var status=TryCaptureTerrainWindow(first,last,candidates,out window);if(status!=RoadQueryStatus.Ready)return status;}
            context=new TerrainBuildContext(this,requests,window,terrainHeightSource.NegativeXLayer,terrainHeightSource.NonNegativeXLayer,terrainHeightSource.LayerRevision);
            terrainBuild=context;return RoadQueryStatus.Ready;
        }
        public bool IsTerrainBuildCurrent(TerrainBuildContext context)=>context!=null&&ReferenceEquals(context,terrainBuild)&&
            !disposed&&AtTickBoundary&&context.Generation==Midline.State.Generation&&context.Revision==Midline.Revision&&
            context.LayerRevision==terrainHeightSource.LayerRevision&&
            (context.Window==null||IsTerrainWindowCurrent(context.Window));
        internal void ReleaseTerrainBuild(TerrainBuildContext context){if(ReferenceEquals(terrainBuild,context))terrainBuild=null;}
        internal bool IsTerrainDataCurrent(int gen,long revision,long layer,bool roads)=>!disposed&&
            Midline.State.Generation==gen&&terrainHeightSource.LayerRevision==layer&&(!roads||(AtTickBoundary&&Midline.Revision==revision));
        internal double ReadTerrainRaw(TerrainBuildContext context,double x,double z)
        {
            if(!IsTerrainBuildCurrent(context))throw new InvalidOperationException("Stale terrain height context");
            return terrainHeight(x,z);
        }
        internal RoadQueryStatus EvaluateTerrain(TerrainBuildContext context,in TerrainHeightInput input,out TerrainHeightResult result)
        {
            result=default;if(!IsTerrainBuildCurrent(context)||context.Window==null)return RoadQueryStatus.NotResident;
            roadBlend??=new HillsRoadBlend(Config.Mesh.LaneStyle);
            return TerrainHeightQuery.TryEvaluate(input,context.Window.Candidates,terrainHeight,roadBlend,out result,context.Window.FirstIndex,context.Window.LastIndex);
        }
        // Extension point for T3: ordered candidates come from cell registration, not a new nearest-road algorithm.
        // One bounded copy per build batch; no producer advance or complete-history snapshot.
        internal RoadQueryStatus TryCaptureTerrainWindow(int first,int last,int[] orderedCandidates,out TerrainQueryWindow window)
        {
            window=null;
            if(disposed||!AtTickBoundary||Spatial==null||Midline.State.Generation!=generation)return RoadQueryStatus.NotResident;
            if((long)last-first<3||(long)last-first>=256)throw new ArgumentOutOfRangeException(nameof(last));
            if(orderedCandidates==null||orderedCandidates.Length==0)return RoadQueryStatus.NoRoad;
            if(orderedCandidates.Length>128)throw new ArgumentOutOfRangeException(nameof(orderedCandidates));
            if(first<Midline.State.Head.Index+1||last>=Midline.AttributeReadyExclusive||last>=Midline.State.FineTail.Index)
                return RoadQueryStatus.NotResident;
            foreach(int i in orderedCandidates)if(i<=first||i>=last-2)return RoadQueryStatus.NotResident;
            var view=Midline.CaptureWindow(first,last);
            window=new TerrainQueryWindow(roadBlendOwner,Spatial.ResidencyRevision,view,orderedCandidates);
            return RoadQueryStatus.Ready;
        }
        internal bool IsTerrainWindowCurrent(TerrainQueryWindow window)=>window!=null&&!window.IsDisposed&&
            ReferenceEquals(window.Owner,roadBlendOwner)&&!disposed&&AtTickBoundary&&Spatial!=null&&
            window.Residency==Spatial.ResidencyRevision&&window.Generation==Midline.State.Generation&&
            window.Revision==Midline.Revision&&window.FirstIndex>=Midline.State.Head.Index+1&&
            window.LastIndex<Midline.AttributeReadyExclusive&&window.LastIndex<Midline.State.FineTail.Index;
        internal RoadQueryStatus TryTerrainHeight(TerrainQueryWindow window,in TerrainHeightInput input,out TerrainHeightResult result)
        {
            result=default;if(!IsTerrainWindowCurrent(window))return RoadQueryStatus.NotResident;
            if(!double.IsFinite(input.X)||!double.IsFinite(input.Z))throw new ArgumentOutOfRangeException(nameof(input));
            double tx=Math.Floor(input.X/Spatial.TileSize),tz=Math.Floor(input.Z/Spatial.TileSize);
            if(tx<int.MinValue||tx>int.MaxValue||tz<int.MinValue||tz>int.MaxValue||Spatial.GetSegmentsForTile((int)tx,(int)tz)==null)
                return RoadQueryStatus.NotResident;
            roadBlend??=new HillsRoadBlend(Config.Mesh.LaneStyle);
            return TerrainHeightQuery.TryEvaluate(input,window.Candidates,terrainHeight,roadBlend,out result,window.FirstIndex,window.LastIndex);
        }
        // Pure world-coordinate query. No producer advance, random consumption or render-origin input.
        public RoadQueryStatus TryRoadBlendedHeight(double x,double z,double baseHeight,out double height,out RoadProjection projection)
        {
            height=default;projection=default;
            if(disposed||!Ready||Spatial==null||Midline.State.Generation!=generation||!Midline.AtUpdateBoundary)return RoadQueryStatus.NotResident;
            if(!double.IsFinite(x)||!double.IsFinite(z)||!double.IsFinite(baseHeight))throw new ArgumentOutOfRangeException();
            var status=Spatial.GetClosestNode(x,z,out int index,out _);if(status!=RoadQueryStatus.Ready)return status;
            if(!RoadProjection.TryCreate(x,z,Midline.FindCoarse(index),out projection))return RoadQueryStatus.NotResident;
            if(projection.Index<Midline.State.Head.Index+1||projection.Index+2>=Midline.AttributeReadyExclusive)return RoadQueryStatus.NotResident;
            projection.Revision=Midline.Revision;
            projection.Owner=roadBlendOwner;
            roadBlend??=new HillsRoadBlend(Config.Mesh.LaneStyle);
            height=roadBlend.Evaluate(baseHeight,ref projection,x,z);return RoadQueryStatus.Ready;
        }
        public bool IsRoadProjectionCurrent(in RoadProjection p)=>ReferenceEquals(p.Owner,roadBlendOwner)&&!disposed&&Ready&&Midline.AtUpdateBoundary&&p.Generation==Midline.State.Generation&&
            p.Index>=Midline.State.Head.Index+1&&p.Index+2<Midline.AttributeReadyExclusive&&
            (p.Revision==Midline.Revision||(p.Revision==Midline.Revision-1&&p.Index+2<Math.Min(Midline.GeometryDirtyFrom,Midline.AttributeDirtyFrom)));
        public MidlineDistanceResult TrySample(double metres,out MidlineDistanceSample sample)
        {
            if(terrainBuild!=null||!Ready||Midline.State.Generation!=Distance.Generation){sample=default;return MidlineDistanceResult.NotReady;}
            var result=Distance.TrySample(metres,out sample);
            if(result==MidlineDistanceResult.Ready&&(!IsRendered(sample.FromCoarseIndex)||sample.ToCoarseIndex>MaxPublishedTail))return MidlineDistanceResult.NotReady;
            return result;
        }
        public bool IsRendered(int coarseIndex)
        {foreach(var s in Slots)if(s.Published&&coarseIndex>=s.PublishedHead&&coarseIndex<s.PublishedTail)return true;return false;}
        public void Dispose(){if(disposed)return;terrainBuild?.Cancel();foreach(var s in Slots){s.Builder.Cancel();s.Input=null;s.Overlay=null;}Distance.Clear();Spatial=null;Midline.Dispose();disposed=true;}
    }
}
