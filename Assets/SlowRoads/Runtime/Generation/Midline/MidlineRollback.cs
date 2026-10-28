using System;
using System.Collections.Generic;

namespace SlowRoads.Generation.Midline
{
    /// <summary>
    /// Se rollback, Dev L01655–L01827. Truncation and tracker reconstruction
    /// yield per node. Retired slots are released only after live cursors move.
    /// </summary>
    internal sealed class MidlineRollback
    {
        readonly MidlineConfig config;
        readonly MidlineState state;
        readonly MidlineNodeStore nodes;
        readonly IMidlineHeightSource height;
        readonly BridgeCandidate bridge;
        readonly List<CoarseNode> retired = new List<CoarseNode>(512);
        int phase, resetIndex, trackerIndex, trackerSteps, releaseIndex;
        public bool RequiresRestart { get; private set; }
        public double RevertDistance { get; private set; }
        internal void Clear()=>retired.Clear();

        public MidlineRollback(MidlineConfig config, MidlineState state, MidlineNodeStore nodes,
            IMidlineHeightSource height, BridgeCandidate bridge)
        { this.config=config; this.state=state; this.nodes=nodes; this.height=height; this.bridge=bridge; }

        public void Begin(MidlineTrackerState tracker, bool turnback, bool overflow,
            bool bridgeViolation, bool shouldBridge, double bridgeRevertNodes)
        {
            RequiresRestart=false;
            RevertDistance=0;
            bool repeatedTurnback=false;
            if(turnback)
            {
                if((state.LastResetIndex>0 && Math.Abs(state.LastResetIndex-state.Tail.Index)<tracker.Window*2) ||
                    (state.RevertTrackerIndex==tracker.Index && state.AntiWeight>0))
                {
                    repeatedTurnback=true;
                    RevertDistance=Math.Max(tracker.Window,state.AntiTargetRevert+tracker.Radius*(state.AntiTargetTriggerCount+1));
                    if(state.Tail.Index-RevertDistance<state.MaxRetractIndex)
                        RevertDistance=state.Tail.Index-state.MaxRetractIndex+(state.AntiTargetTriggerCount+1);
                }
                else RevertDistance=tracker.Window;
            }
            else if(overflow)
            {
                if(state.AntiWeight>0) state.AntiTargetRevert+=50;
                else state.AntiTargetRevert=25;
            }
            else if(bridgeViolation) RevertDistance=state.Tail.Index-bridge.SpawnNode.Index+5;

            if(state.Bridging)
            {
                shouldBridge=false;
                state.Bridging=false;
                state.AntiBridgePoint=bridge.Landing;
                bridge.Reset();
            }
            if(shouldBridge || (bridge.SpawnNode!=null && bridge.Quality>0 && bridgeRevertNodes<RevertDistance))
            {
                state.Bridging=true;
                state.OnBridgeApproach=true;
                state.BridgeApproachFactor=1;
                state.OnBridgeDismount=false;
                BridgePlanner.ImproveStartNode(bridge);
                bridge.Landing=bridge.Landing.WithY(bridge.SpawnNode.Position.Y);
                bridge.HasHeight=true;
                bridge.Height=bridge.Landing.Y;
                state.RevertTrackerIndex=null;
                state.AntiTargetRange=0;
                state.AntiTargetTrigger=state.Tail.Index;
                RevertDistance=state.Tail.Index-bridge.SpawnNode.Index;
            }
            else if(overflow)
            {
                UpdateMaxRetract();
                RevertDistance=Math.Max(50,state.AntiTargetRevert);
                UpdateAntiTarget();
                state.AllowEmergencyBridge=true;
            }
            else
            {
                state.AntiTargetTriggerPoint=state.Tail.Position;
                if(repeatedTurnback)
                {
                    state.AntiTargetTriggerCount++;
                    state.AntiTargetRevert=Math.Max(tracker.Window,
                        state.AntiTargetRevert+tracker.Radius*state.AntiTargetTriggerCount);
                    if(state.Tail.Index-state.AntiTargetRevert<state.MaxRetractIndex)
                    {
                        state.AntiTarget=state.AntiTarget.Lerp(state.Tail.Position,state.Random.Next());
                        state.AntiTargetRevert=state.Tail.Index-state.MaxRetractIndex+state.AntiTargetTriggerCount;
                        state.AllowEmergencyBridge=true;
                    }
                    else { UpdateMaxRetract(); UpdateAntiTarget(); }
                    RevertDistance=state.AntiTargetRevert;
                }
                else
                {
                    if(tracker==null) throw new InvalidOperationException("Source rollback requires a tracker.");
                    state.RevertTrackerIndex=tracker.Index;
                    Double3 a=state.Tail.Position,b=tracker.Node.Position;
                    state.AntiTarget=new Double3(a.X+b.X,a.Y+b.Y,a.Z+b.Z).Multiply(0.5);
                    state.AntiTargetRevert=tracker.Window;
                    state.AntiTargetTriggerPoint=state.AntiTarget;
                    state.AntiTargetTriggerCount=0;
                    RevertDistance=state.AntiTargetRevert;
                }
            }
            resetIndex=state.Tail.Index-(int)RevertDistance;
            RequiresRestart=resetIndex<state.Head.Index+config.SmoothWindow;
            phase=0; trackerIndex=trackerSteps=releaseIndex=0;
            retired.Clear();
        }

        void UpdateMaxRetract()
        {
            if(state.AntiTargetRevert>=500) { state.MaxRetractCount++; state.AntiTargetRevert=500; }
            else if(state.MaxRetractCount!=0) state.MaxRetractCount=0;
        }
        void UpdateAntiTarget()
        {
            if(state.MaxRetractCount!=0)
            {
                Double3 p=state.Tail.Position;
                double x=p.X+state.Random.Next()*state.AntiTargetRevert*Math.Min(state.MaxRetractCount,4);
                double z=p.Z+state.Random.Next()*state.AntiTargetRevert*Math.Min(state.MaxRetractCount,4);
                state.AntiTarget=new Double3(x,p.Y,z);
            }
            else state.AntiTarget=state.AntiTarget.Lerp(state.Tail.Position,1.0/(state.AntiTargetTriggerCount+1));
        }

        public bool Advance()
        {
            if(RequiresRestart)
            {
                if(releaseIndex<retired.Count) { nodes.Release(retired[releaseIndex++]); return false; }
                retired.Clear(); return true;
            }
            if(phase==0)
            {
                CoarseNode tail=state.Tail;
                bool stop=tail.Index<=resetIndex;
                if(!stop && !tail.Bridge && tail.Previous!=null && tail.Previous.Bridge)
                {
                    CoarseNode before=tail.Previous;
                    while(before!=null && before.Bridge) before=before.Previous;
                    if(before==null) throw new InvalidOperationException("Bridge has no preceding ordinary node.");
                    if(before.Index<resetIndex) stop=true;
                    else state.AntiBridgePoint=tail.Position;
                }
                if(!stop && (tail.Previous==null || tail.Index-state.Head.Index<config.SmoothWindow ||
                    (tail.HasTargetDot && tail.TargetDot>0 && tail.GradeForwardAverage<0.1 && Math.Abs(tail.Grade)<0.1 &&
                    Math.Abs(tail.Index-state.LastResetIndex)>state.AntiTargetRevert/4))) stop=true;
                if(!stop)
                {
                    if(tail.HasControlSnapshot && tail.ControlSnapshot!=0)
                        state.AccumulatedOverflow-=Math.Abs(tail.ControlSnapshot);
                    state.TotalHeadingDelta-=Math.Abs(tail.HeadingDelta);
                    state.Tail=tail.Previous;
                    state.Tail.Next=null;
                    retired.Add(tail);
                    return false;
                }
                phase=1;
            }
            if(phase==1)
            {
                var layer=state.TailLayers;
                if(state.Tail.Index<layer.PreviousSwitchNode)
                {
                    layer.PreviousSide=-layer.PreviousSide;
                    layer.NextLayerSwitch=-layer.NextLayerSwitch;
                    if(layer.CurrentSide==0) layer.NegativeX--; else layer.NonNegativeX--;
                    layer.CurrentSide=(layer.CurrentSide+1)%2;
                    height.SetLayers(layer.NegativeX,layer.NonNegativeX);
                    layer.PreviousSwitchNode=-100;
                }
                CoarseNode window=state.SmoothWindowHead;
                MidlineSmoothing.RestoreHeightAfterRollback(ref window,state.Tail,config.SmoothWindow);
                state.SmoothWindowHead=window;
                if(!state.Bridging)
                {
                    state.AntiTargetRange=DistanceSquared(state.AntiTarget,state.Tail.Position)*4;
                    state.LastResetIndex=state.Tail.Index;
                }
                if(!state.Bridging && bridge.SpawnNode!=null && bridge.SpawnNode.Index>state.Tail.Index) bridge.Reset();
                if(state.ObserverIndex>state.Tail.Index)
                {
                    state.ObserverIndex=state.Tail.Index;
                    state.ObserverNode=state.Tail;
                    RequiresRestart=true;
                    return false;
                }
                phase=2;
            }
            if(phase==2)
            {
                if(trackerIndex<state.Trackers.Length)
                {
                    var tracker=state.Trackers[trackerIndex];
                    if(trackerSteps==0) { tracker.Node=state.Tail; tracker.Delta=state.Tail.HeadingDelta; }
                    if(trackerSteps<tracker.Window && tracker.Node.Previous!=null)
                    {
                        tracker.Node=tracker.Node.Previous;
                        tracker.Delta+=tracker.Node.HeadingDelta;
                        trackerSteps++;
                        return false;
                    }
                    trackerIndex++; trackerSteps=0;
                    return false;
                }
                RestoreControls(); phase=3;
            }
            if(releaseIndex<retired.Count) { nodes.Release(retired[releaseIndex++]); return false; }
            retired.Clear();
            return true;
        }
        void RestoreControls()
        {
            CoarseNode tail=state.Tail;
            state.PreviousTurn=tail.Turn;
            state.WaterFactor=Math.Max(Math.Min(1,1-(tail.Position.Y-config.MinimumElevation)/(config.MinimumElevation*2)),0);
            if(tail.HasControlSnapshot)
            {
                state.PreviousTargetSide=tail.ControlSnapshot<0?-1:1;
                state.TargetOverflow=Math.Abs(tail.ControlSnapshot);
            }
            else
            {
                state.MidlineDirection=new Double3(MidlineTrig.Cos(tail.Heading),0,MidlineTrig.Sin(tail.Heading));
                state.MidlineOrthogonal=new Double3(-state.MidlineDirection.Z,0,state.MidlineDirection.X);
                state.PreviousTargetSide=Math.Sign(state.MidlineOrthogonal.Dot(state.TargetDirection)*-1);
                state.TargetOverflow=state.AccumulatedOverflow=0;
                state.InOverflow=false;
            }
        }
        static double DistanceSquared(Double3 a,Double3 b)
        { double x=a.X-b.X,y=a.Y-b.Y,z=a.Z-b.Z; return x*x+y*y+z*z; }
    }
}
