using System;
using System.Collections.Generic;

namespace SlowRoads.Generation.Midline
{
    public enum MidlineAdvanceResult
    {
        Yielded, CoarseAdded, Reverted, Restarted, FineAdvanced, NoWork
    }

    enum MidlineUpdatePhase { Initialising, Prepare, BridgeSweep, Tracker, Commit, Smooth, Rollback, Post, Fine, Shadow, Markings }

    /// <summary>
    /// Incremental Se update and B7 coarse/fine attributes. Yielded state is
    /// provisional; publication/lifetime validation is a separate contract.
    /// </summary>
    public sealed class MidlineSession : IDisposable
    {
        readonly MidlineConfig config;
        readonly IMidlineHeightSource height;
        readonly MidlineInitialization initialisation;
        readonly MidlineState state;
        readonly MidlineNodeStore nodes;
        readonly SteeringDraft draft = new SteeringDraft();
        readonly BridgeCandidate bridge = new BridgeCandidate();
        readonly MidlineRollback rollback;
        readonly HillsBlendNoise blendNoise=new HillsBlendNoise();
        readonly HillsShadowDensitySource shadowSource;
        readonly HillsMidlineAttributes attributes;
        readonly FineMidlineSampler fineSampler=new FineMidlineSampler();
        readonly Action<int,int> headLayerChanged;
        MidlineMarkingsState markings;
        MidlineAdvanceResult completedResult;
        BridgeCandidateSweep sweep;
        MidlineUpdatePhase phase = MidlineUpdatePhase.Initialising;
        int trackerIndex, updateOrdinal;
        bool shouldBridge, restarting;
        double bridgeRevertNodes;
        bool disposed;
        bool fineChanged;
        readonly string heightSeed;
        readonly bool ownsHeight;
        readonly bool recordObserverInputs;
        readonly bool resetMarkingsOnRestart;
        readonly List<MidlineObserverInput> observerInputs=new List<MidlineObserverInput>();
        string seedInput;
        int startNodeInput;
        public long Revision {get;private set;}
        public int GeometryDirtyFrom {get;private set;}=int.MaxValue;
        public int AttributeDirtyFrom {get;private set;}=int.MaxValue;

        public MidlineSession(MidlineConfig config, string heightSeed,
            IMidlineHeightSource height, Action<int, int> headLayerChanged = null,bool ownsHeight=false,bool recordObserverInputs=true,bool resetMarkingsOnRestart=false)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.height = height ?? throw new ArgumentNullException(nameof(height));
            this.heightSeed=heightSeed;this.ownsHeight=ownsHeight;
            this.recordObserverInputs=recordObserverInputs;
            this.resetMarkingsOnRestart=resetMarkingsOnRestart;
            this.headLayerChanged=headLayerChanged;
            shadowSource=new HillsShadowDensitySource(config.Style,heightSeed);
            attributes=new HillsMidlineAttributes(shadowSource.GetShadowDensityAt,config.RoadHalfWidth);
            initialisation = new MidlineInitialization(config, heightSeed, height, NotifyHeadLayers,
                beforeRetire:()=>nodes.PinRestartReferences(markings?.Node,state.Head?.NextFine));
            state = initialisation.State;
            nodes = initialisation.Nodes;
            nodes.RetainReference=IsReferenced;
            rollback = new MidlineRollback(config, state, nodes, height, bridge);
        }

        public MidlineState State {get{ThrowIfDisposed();return state;}}
        public BridgeCandidate Bridge {get{ThrowIfDisposed();return bridge;}}
        public int UpdateOrdinal => updateOrdinal;
        public int AllocatedCoarseNodeCount {get{ThrowIfDisposed();return nodes.AllocatedCount;}}
        public MidlineMarkingsState Markings {get{ThrowIfDisposed();return markings;}}
        public bool AtUpdateBoundary=>phase==MidlineUpdatePhase.Prepare;
        public CoarseNode FindCoarse(int index)=>nodes.Find(index,state.Generation);
        public int AttributeReadyExclusive=>Math.Max(state.Head.Index,Math.Min(state.FineTail.Index,
            (markings?.Node.Index??state.Head.Index)-(int)Math.Ceiling(11+config.MarkingsMargin/2)));
        public MidlineReadView CaptureWindow(int first,int last)
        {
            ThrowIfDisposed();if(!AtUpdateBoundary)throw new InvalidOperationException("Window requires completed update");
            var node=FindCoarse(first);if(node==null)throw new ArgumentOutOfRangeException(nameof(first));
            return new MidlineReadView(state,markings,bridge,Revision,GeometryDirtyFrom,AttributeDirtyFrom,
                (int)Math.Ceiling(11+config.MarkingsMargin/2),node,last);
        }
        public bool IsCurrent(MidlineReadView view)=>!disposed && AtUpdateBoundary && view!=null &&
            view.Generation==state.Generation && view.Revision==Revision;
        public MidlineReadView CaptureReadView()
        {
            ThrowIfDisposed();
            if(!AtUpdateBoundary)throw new InvalidOperationException("Read at a completed logical update boundary.");
            return new MidlineReadView(state,markings,bridge,Revision,GeometryDirtyFrom,AttributeDirtyFrom,
                (int)Math.Ceiling(11+config.MarkingsMargin/2));
        }
        public void SetObserverIndex(int index)
        {
            ThrowIfDisposed();
            if(!AtUpdateBoundary)throw new InvalidOperationException("Set observer input at a completed update boundary.");
            if(index<state.Head.Index || index>state.Tail.Index)
                throw new ArgumentOutOfRangeException(nameof(index),"Observer progress must be inside the retained coarse path.");
            state.ObserverIndex=index;
            var observer=FindCoarse(index)??throw new InvalidOperationException("Observer node is not retained");
            state.ObserverNode=observer;
            if(!recordObserverInputs)return;
            if(observerInputs.Count>0 && observerInputs[observerInputs.Count-1].UpdateOrdinal==updateOrdinal)
                observerInputs[observerInputs.Count-1]=new MidlineObserverInput(updateOrdinal,index);
            else observerInputs.Add(new MidlineObserverInput(updateOrdinal,index));
        }

        public void Begin(string seed, int startNode)
        {ThrowIfDisposed();initialisation.Begin(seed,startNode);seedInput=seed;startNodeInput=startNode;}
        public MidlineReplayCheckpoint CreateReplayCheckpoint()
        {
            ThrowIfDisposed();if(!AtUpdateBoundary)throw new InvalidOperationException("Checkpoint requires completed update.");
            if(!recordObserverInputs)throw new InvalidOperationException("Replay history recording is disabled for this streaming session");
            return new MidlineReplayCheckpoint(config,seedInput,heightSeed,startNodeInput,updateOrdinal,
                observerInputs.ToArray(),ComputeDigest());
        }
        public ulong ComputeDigest()
        {
            var view=CaptureReadView();ulong hash=14695981039346656037UL;
            void Mix(long x){unchecked{hash=(hash^(ulong)x)*1099511628211UL;}}
            void Number(double x)=>Mix(BitConverter.DoubleToInt64Bits(x));
            void Vector(Double3 v){Number(v.X);Number(v.Y);Number(v.Z);}
            Mix(view.Generation);Mix(updateOrdinal);Mix(state.ObserverIndex);
            for(int i=0;i<view.CoarseCount;i++)
            {
                var c=view.GetCoarse(i);Mix(c.Index);Vector(c.Position);Vector(c.Side);
                Number(c.Blend);Number(c.Bank);Number(c.HeightCorrection);Number(c.Grade);Number(c.GradeFiltered);
                Number(c.PaintLeft);Number(c.PaintRight);Mix(c.HasPaintLeft?1:0);Mix(c.HasPaintRight?1:0);
                Mix(c.MergeLeft?1:0);Mix(c.MergeRight?1:0);Mix(c.Bridge?1:0);
            }
            for(int i=0;i<view.FineCount;i++)
            {var f=view.GetFine(i);Mix(f.CoarseIndex);Mix(f.Ordinal);Vector(f.Position);Vector(f.Side);
                Number(f.ShadowLeft);Number(f.ShadowRight);Mix(f.HasShadowLeft?1:0);Mix(f.HasShadowRight?1:0);}
            var random=state.Random.ExportState();Number(random.S0);Number(random.S1);Number(random.S2);Mix(random.Carry);
            foreach(var t in state.Trackers){Mix(t.Node.Index);Number(t.Delta);Number(t.DeltaThreshold);}
            Number(state.PreviousTurn);Vector(state.AntiTarget);Number(state.AntiTargetRange);
            Mix(markings.Node.Index);Number(markings.Da);Number(markings.AbsDa);Number(markings.Trees);
            Number(markings.GradLat);Number(markings.PreviousDa);Number(markings.PreviousGradLonDiff);
            return hash;
        }

        public MidlineAdvanceResult Advance(int budget)
        {
            if(disposed)throw new ObjectDisposedException(nameof(MidlineSession));
            if (budget < 1) throw new ArgumentOutOfRangeException(nameof(budget));
            while (budget-- > 0)
            {
                switch (phase)
                {
                    case MidlineUpdatePhase.Initialising:
                        if (initialisation.Advance(1))
                        {
                            phase = MidlineUpdatePhase.Prepare;
                            // Compatibility fix authorized for the production stream: native B7
                            // retains old-generation markings and eventually dereferences null.
                            // Raw algorithm fixtures keep the original contract via the default false.
                            if(restarting&&resetMarkingsOnRestart){markings?.Clear();markings=null;}
                            if(markings==null)markings=new MidlineMarkingsState(config,state.Head);
                            if (restarting)
                            {
                                restarting=false;GeometryDirtyFrom=AttributeDirtyFrom=int.MinValue;
                                completedResult=MidlineAdvanceResult.Restarted;return CompleteUpdate();
                            }
                        }
                        break;
                    case MidlineUpdatePhase.Prepare:
                        fineChanged=false;
                        GeometryDirtyFrom=AttributeDirtyFrom=int.MaxValue;
                        if (state.Tail.Index >= state.ObserverIndex + 2500)
                        {completedResult=MidlineAdvanceResult.NoWork;phase=MidlineUpdatePhase.Post;break;}
                        MidlineSteering.Prepare(config, state, height, draft, deferCompletion:true);
                        if (draft.NeedsBridgeCandidate)
                        {
                            sweep = new BridgeCandidateSweep(config, state, draft, height, bridge);
                            sweep.Begin();
                            phase = MidlineUpdatePhase.BridgeSweep;
                        }
                        else { DecideBridge(); phase = MidlineUpdatePhase.Tracker; }
                        break;
                    case MidlineUpdatePhase.BridgeSweep:
                        if (sweep.Advance(1))
                        {
                            sweep = null;
                            DecideBridge();
                            phase = MidlineUpdatePhase.Tracker;
                        }
                        break;
                    case MidlineUpdatePhase.Tracker:
                        if (trackerIndex < state.Trackers.Length)
                        {
                            int trackerIndexBefore=trackerIndex;
                            bool violated = UpdateTracker(state.Trackers[trackerIndex++]);
                            if (violated)
                            {
                                trackerIndex = state.Trackers.Length; // Se breaks even on straight profiles.
                                if (!config.IsStraight)
                                {
                                    BeginRollback(state.Trackers[trackerIndexBefore], true);
                                    break;
                                }
                            }
                        }
                        if (trackerIndex >= state.Trackers.Length)
                        {
                            if (!config.IsStraight && (draft.ViolatedOverflow || shouldBridge))
                            {
                                BeginRollback(state.Trackers[state.Trackers.Length-1], false);
                                break;
                            }
                            if (!config.IsStraight && state.Bridging &&
                                state.Tail.Index-bridge.SpawnNode.Index>bridge.Distance/10+20)
                            { BeginRollback(state.Trackers[state.Trackers.Length-1], false, true); break; }
                            phase = MidlineUpdatePhase.Commit;
                        }
                        break;
                    case MidlineUpdatePhase.Commit:
                        CommitOrdinary();
                        phase = MidlineUpdatePhase.Smooth;
                        break;
                    case MidlineUpdatePhase.Smooth:
                        SmoothAndLayers();
                        completedResult=MidlineAdvanceResult.CoarseAdded;phase=MidlineUpdatePhase.Post;
                        break;
                    case MidlineUpdatePhase.Rollback:
                        if (rollback.Advance())
                        {
                            trackerIndex=0;
                            if (rollback.RequiresRestart)
                            {
                                // deleteState clears only spawn/quality; distance/landing survive.
                                bridge.SpawnNode=null; bridge.Quality=0;
                                initialisation.RestartGeneration(); restarting=true;
                                phase=MidlineUpdatePhase.Initialising;
                            }
                            else
                            {
                                // Se.update still checks height after extendMidline returns false.
                                if (state.SmoothWindowHead.Index<state.Tail.Index-config.SmoothWindow) SmoothOnce(false);
                                completedResult=MidlineAdvanceResult.Reverted;phase=MidlineUpdatePhase.Post;
                            }
                        }
                        break;
                    case MidlineUpdatePhase.Post:
                        if(completedResult==MidlineAdvanceResult.CoarseAdded && state.Tail.Index-state.Head.Index>2522)
                        {
                            var retired=state.Head;
                            state.Head=state.Head.Next;state.Head.Previous=null;state.Head.PreviousFine=null;
                            nodes.Release(retired);
                            CheckHeadLayer();
                        }
                        if(FineIsRequired())
                        {
                            fineSampler.Begin(state.FineTail,config.MinFineSegments,config.CurvatureHeightFactor);
                            phase=MidlineUpdatePhase.Fine;
                        }
                        else return CompleteUpdate();
                        break;
                    case MidlineUpdatePhase.Fine:
                        if(fineSampler.Advance(1))
                        {
                            state.FineTail=fineSampler.NextCoarse;
                            fineChanged=true;
                            attributes.Begin(state.FineTail.Previous);phase=MidlineUpdatePhase.Shadow;
                        }
                        break;
                    case MidlineUpdatePhase.Shadow:
                        if(attributes.Advance(1)){markings.Begin(state.FineTail.Previous);phase=MidlineUpdatePhase.Markings;}
                        break;
                    case MidlineUpdatePhase.Markings:
                        if(markings.Advance(1))
                        {
                            if(completedResult==MidlineAdvanceResult.NoWork)completedResult=MidlineAdvanceResult.FineAdvanced;
                            return CompleteUpdate();
                        }
                        break;
                    default:
                        throw new InvalidOperationException("Invalid update phase " + phase);
                }
            }
            return MidlineAdvanceResult.Yielded;
        }

        MidlineAdvanceResult CompleteUpdate()
        {
            if(completedResult==MidlineAdvanceResult.CoarseAdded || completedResult==MidlineAdvanceResult.Reverted)
                GeometryDirtyFrom=Math.Min(GeometryDirtyFrom,
                    Math.Min(state.SmoothWindowHead.Index-2,state.Tail.Index-config.SmoothWindow-2));
            if(fineChanged)
            {
                GeometryDirtyFrom=Math.Min(GeometryDirtyFrom,state.FineTail.Index-1);
                AttributeDirtyFrom=Math.Min(attributes.DirtyFromCoarseIndex,markings.DirtyFromCoarseIndex);
            }
            Revision++;
            nodes.AdvancePins(markings?.Node,state.Head.NextFine);
            updateOrdinal++;trackerIndex=0;phase=MidlineUpdatePhase.Prepare;return completedResult;
        }
        void ThrowIfDisposed(){if(disposed)throw new ObjectDisposedException(nameof(MidlineSession));}
        bool IsReferenced(CoarseNode node)
        {
            if(node==state.Head||node==state.Tail||node==state.FineTail||node==state.SmoothWindowHead||
                node==state.ObserverNode||node==bridge.SpawnNode)return true;
            foreach(var tracker in state.Trackers)if(node==tracker.Node)return true;
            if(markings==null)return false;
            if(node==markings.Node||node==markings.LeftStart||node==markings.RightStart)return true;
            // Original B7 may walk from old segment ends. Retain the bounded merge/
            // prepend neighborhood even across restart; do not reuse those objects early.
            int guard=(int)Math.Ceiling(11+config.MarkingsMargin/2);
            var left=markings.LeftEnd;var right=markings.RightEnd;
            for(int i=0;i<=guard;i++)
            {
                if(node==left||node==right)return true;
                left=left?.Next;right=right?.Next;
            }
            return false;
        }
        bool FineIsRequired()=>state.FineTail.Index<state.ObserverIndex+config.FineHorizon && state.FineTail.Index<state.Tail.Index-1900;
        void NotifyHeadLayers(int negativeX,int nonNegativeX)
        {
            shadowSource.SetHeadLayers(negativeX,nonNegativeX);headLayerChanged?.Invoke(negativeX,nonNegativeX);
        }
        void CheckHeadLayer()
        {
            if(!config.Spiralise)return;
            var layer=state.HeadLayers;int z=Math.Sign(state.Head.Position.Z),x=Math.Sign(state.Head.Position.X);
            if(layer.PreviousSide==z||layer.NextLayerSwitch!=x)return;
            layer.PreviousSide=z;layer.NextLayerSwitch=-x;layer.CurrentSide=(layer.CurrentSide+1)%2;
            if(layer.CurrentSide==0)layer.NegativeX++;else layer.NonNegativeX++;
            NotifyHeadLayers(layer.NegativeX,layer.NonNegativeX);
        }

        void DecideBridge()
        {
            MidlineSteering.FinishPreparation(config, state, draft);
            shouldBridge = false;
            if (state.Bridging || bridge.SpawnNode == null) return;
            double revertNodes = state.Tail.Index - bridge.SpawnNode.Index;
            bridgeRevertNodes = revertNodes;
            if (revertNodes > 500) bridge.Reset();
            if (bridge.SpawnNode == null) return;
            double threshold = draft.TargetDot < 0
                ? 0.5 + draft.TargetDot / 2 : 0.5 + draft.TargetDot * 0.25;
            threshold -= state.AntiTargetTriggerCount * 0.1;
            if (state.TargetOverflow > 0) threshold = 0;
            double minimum = (1 - bridge.Quality) * 20;
            double maximum = (1 - threshold) * 150;
            shouldBridge = bridge.Quality >= threshold &&
                revertNodes >= minimum && revertNodes <= maximum;
        }

        void BeginRollback(MidlineTrackerState tracker, bool turnback, bool bridgeViolation=false)
        {
            rollback.Begin(tracker,turnback,draft.ViolatedOverflow,bridgeViolation,shouldBridge,bridgeRevertNodes);
            phase=MidlineUpdatePhase.Rollback;
        }

        bool UpdateTracker(MidlineTrackerState tracker)
        {
            CoarseNode tail = state.Tail;
            tracker.Delta += tail.HeadingDelta;
            if (tracker.Node.Index <= tail.Index - tracker.Window)
            {
                tracker.Delta -= tracker.Node.HeadingDelta;
                tracker.Node = tracker.Node.Next;
            }
            tracker.DeltaThreshold = tracker.Limit;
            if (Math.Abs(tracker.Delta) > tracker.DeltaThreshold)
            {
                var from = tracker.Node.Position;
                var direction = new Double3(tail.Position.X - from.X,
                    tail.Position.Y - from.Y, tail.Position.Z - from.Z).Normalize();
                tracker.TargetDot = direction.Dot(state.TargetDirection);
                tracker.DeltaThreshold = tracker.Limit;
                tracker.Tolerance = (tracker.TargetDot + draft.TargetDot) / 2;
                if (((draft.TargetDot < 0 || tracker.TargetDot < 0) && state.ByWater) ||
                    state.TargetOverflow > 0)
                    tracker.Tolerance = 0;
                if (tracker.Tolerance > 0)
                    tracker.DeltaThreshold = tracker.Limit +
                        tracker.Tolerance * tracker.ToleranceFactor;
            }
            return !state.Bridging &&
                (Math.Abs(tracker.Delta) > tracker.DeltaThreshold || shouldBridge ||
                (tracker.GradeThreshold.HasValue && Math.Abs(tail.Grade) > tracker.GradeThreshold.Value) ||
                Math.Abs(draft.LongitudinalGrade) > config.MaximumLongitudinalGrade);
        }

        void CommitOrdinary()
        {
            CoarseNode previous = state.Tail;
            if (state.Bridging)
            {
                if (state.OnBridgeApproach)
                {
                    Double3 direction=new Double3(bridge.Landing.X-bridge.SpawnNode.Position.X,0,
                        bridge.Landing.Z-bridge.SpawnNode.Position.Z).Normalize();
                    double delta=previous.Heading-MidlineTrig.Atan2(direction.Z,direction.X);
                    if(delta < -Math.PI) delta+=Math.PI*2;
                    else if(delta>Math.PI) delta-=Math.PI*2;
                    draft.ProposedTurn=Math.Min(Math.Max(-state.BridgeApproachFactor,delta/config.FeelAngle),state.BridgeApproachFactor);
                    if(state.BridgeApproachFactor<1.4) state.BridgeApproachFactor=1.4;
                    if(Math.Abs(draft.ProposedTurn)<0.25) draft.ProposedTurn=0;
                }
                else draft.ProposedTurn=0;
            }
            CoarseNode node = nodes.Acquire(state.Generation);
            MidlineSteering.PredictOrdinaryNode(config, state, height, draft,
                node, bridgeResolved:true);
            state.PreviousTurn = draft.FinalTurn;
            double dx = draft.NextX - previous.Position.X;
            double dz = draft.NextZ - previous.Position.Z;
            double horizontalLength = Math.Sqrt(dx * dx + dz * dz);
            previous.Normal = new Double3(-dz / horizontalLength, 0, dx / horizontalLength);
            state.TotalHeadingDelta += Math.Abs(node.HeadingDelta);
            if (state.Bridging)
            {
                if(state.OnBridgeApproach)
                {
                    if(node.Position.Y<=bridge.Height && draft.FinalTurn==0)
                    { previous.Bridge=true; previous.HasBridge=true; state.OnBridgeApproach=false; }
                    node.Position=node.Position.WithY(node.Position.Y>bridge.Height ?
                        (bridge.Height+node.Position.Y)/2 : Math.Max(node.Position.Y,bridge.Height));
                }
                else if(node.Position.Y>config.MinimumElevation && node.Position.Y>bridge.Height-1)
                {
                    state.OnBridgeDismount=false; state.Bridging=false; state.AllowEmergencyBridge=false;
                    bridge.Reset();
                    node.Position=node.Position.WithY((node.Position.Y+bridge.Height)/2);
                }
                else node.Position=node.Position.WithY(Math.Max(node.Position.Y,bridge.Height));
            }
            if((state.Bridging && !state.OnBridgeApproach && !state.OnBridgeDismount) ||
                (config.IsStraight && node.Position.Y<=2)) { node.Bridge=true; node.HasBridge=true; }
            if (state.TargetOverflow > 0)
            {
                node.HasControlSnapshot = true;
                node.ControlSnapshot = state.TargetOverflow * draft.TargetSide;
            }
            previous.Next = node;
            node.Previous = previous;
            nodes.Register(node);
            state.Tail = node;
            state.MaxRetractIndex = Math.Max(state.MaxRetractIndex, node.Index - 500);
            UpdateTargetDirection(node);
        }

        void UpdateTargetDirection(CoarseNode node)
        {
            if (!config.Spiralise) return;
            Double3 radial = node.Position.Negate().Normalize();
            state.TargetDirection = new Double3(-radial.Z * state.SpiralDirection, 0,
                radial.X * state.SpiralDirection).Normalize();
            if (config.IsStraight) return;
            double radius = node.Position.Length(); // Original uses 3D radius.
            double factor;
            if (radius < state.TargetRadius)
            {
                factor = radius < state.TargetRadiusMin ? 1 :
                    1 - (radius - state.TargetRadiusMin) / state.TargetMargin;
                factor *= factor;
                state.TargetDirection = state.TargetDirection.Lerp(radial.Negate(), factor).Normalize();
            }
            else
            {
                factor = radius > state.TargetRadiusMax ? 1 :
                    (radius - state.TargetRadius) / state.TargetMargin;
                factor *= factor;
                state.TargetDirection = state.TargetDirection.Lerp(radial, factor).Normalize();
            }
        }

        void SmoothAndLayers()
        {
            if (state.SmoothWindowHead.Index < state.Tail.Index - config.SmoothWindow)
                SmoothOnce(true);
            if (config.Spiralise) CheckTailLayer();
            blendNoise.Apply(state.Tail); // B7 after base extend succeeds, before Se.update's second height check.
            // Se.update checks the same condition a second time after extendMidline.
            if (state.SmoothWindowHead.Index < state.Tail.Index - config.SmoothWindow)
                SmoothOnce(false);
        }

        void SmoothOnce(bool curves)
        {
            CoarseNode head = state.SmoothWindowHead;
            double headHeight = state.SmoothMidlineHeadHeight;
            MidlineSmoothing.SmoothHeightAndBank(ref head, state.Head,
                config.SmoothWindow, ref headHeight);
            state.SmoothWindowHead = head;
            state.SmoothMidlineHeadHeight = headHeight;
            if (curves) MidlineSmoothing.SmoothCurves(head, config.SmoothingStrength);
        }

        void CheckTailLayer()
        {
            var layer = state.TailLayers;
            int sideZ = Math.Sign(state.Tail.Position.Z);
            int sideX = Math.Sign(state.Tail.Position.X);
            if (layer.PreviousSide == sideZ || layer.NextLayerSwitch != sideX) return;
            layer.PreviousSide = sideZ;
            layer.NextLayerSwitch = sideX * -1;
            layer.CurrentSide = (layer.CurrentSide + 1) % 2;
            if (layer.CurrentSide == 0) layer.NegativeX++;
            else layer.NonNegativeX++;
            height.SetLayers(layer.NegativeX, layer.NonNegativeX);
            layer.PreviousSwitchNode = state.Tail.Index;
            layer.HasPreviousSwitchNode = true;
        }
        // External height input remains caller-owned. Only the private shadow source is owned here.
        public void Dispose()
        {
            if(disposed)return;
            shadowSource.Dispose();if(ownsHeight && height is IDisposable owner)owner.Dispose();
            fineSampler.Clear();attributes.Clear();markings?.Clear();rollback.Clear();
            sweep=null;bridge.Reset();nodes.Clear();observerInputs.Clear();
            state.Head=state.Tail=state.FineTail=state.ObserverNode=state.SmoothWindowHead=null;
            foreach(var t in state.Trackers)t.Node=null;
            state.Generation++;disposed=true;
        }
    }
}
