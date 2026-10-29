using System;
using SlowRoads.Generation.Heightfield;
using SlowRoads.Generation.Random;

namespace SlowRoads.Generation.Midline
{
    public interface IMidlineHeightSource
    {
        double GetXZ(double x, double z);
        void SetLayers(int negativeX, int nonNegativeX, bool force = false);
    }

    /// <summary>Owns the P1 source only when this adapter is used by a session.</summary>
    public sealed class HillsMidlineHeightSource : IMidlineHeightSource, IDisposable
    {
        readonly HillsHeightfield field;
        public HillsMidlineHeightSource(HillsHeightfieldConfig config) => field = new HillsHeightfield(config);
        public double GetXZ(double x, double z) => field.GetXZ(x, z);
        public void SetLayers(int negativeX, int nonNegativeX, bool force = false) =>
            field.SetLayers(negativeX, nonNegativeX, force);
        public void Dispose() => field.Dispose();
    }

    public enum MidlineInitializationPhase
    {
        NotStarted, ChooseCandidate, QueryElevation, QueryMinusZ, QueryPlusX,
        QueryPlusZ, ResolveHeading, CommitHead, Complete
    }

    /// <summary>
    /// Resumable Se.initialise/getNextStartingPose/makeHeadNode, Dev L01042–L01067,
    /// L01102–L01167, L01221–L01324. One Advance unit runs one phase, so no
    /// height query or random draw is replayed when the caller changes budget.
    /// </summary>
    public sealed class MidlineInitialization
    {
        readonly MidlineConfig config;
        readonly IMidlineHeightSource height;
        readonly Action<int, int> headLayerChanged;
        readonly Action beforeRetire;
        readonly MidlineNodeStore nodes;
        readonly MidlineState state = new MidlineState();
        MidlineInitializationPhase phase;
        double candidateX, candidateZ, elevation, minusZ, plusX, plusZ, radiusOffset;
        bool dedicatedStart;
        int queryCount, layerEventCount;

        public MidlineInitialization(MidlineConfig config, string heightSeed,
            IMidlineHeightSource height, Action<int, int> headLayerChanged = null,
            MidlineNodeStore nodes = null,Action beforeRetire=null)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.height = height ?? throw new ArgumentNullException(nameof(height));
            this.headLayerChanged = headLayerChanged;
            this.beforeRetire=beforeRetire;
            this.nodes = nodes ?? new MidlineNodeStore();
            state.ConstructorRandom = new AleaRandom(heightSeed ?? throw new ArgumentNullException(nameof(heightSeed)));
            state.SpiralDirection = config.OneDir ? 1 : (state.ConstructorRandom.Next() < 0.5 ? -1 : 1);
            phase = MidlineInitializationPhase.NotStarted;
        }

        public MidlineState State => state;
        internal MidlineNodeStore Nodes => nodes;
        public MidlineInitializationPhase Phase => phase;
        public int QueryCount => queryCount;
        public int LayerEventCount => layerEventCount;
        public bool IsComplete => phase == MidlineInitializationPhase.Complete;

        public void Begin(string seed, int startNode)
        {
            if (phase != MidlineInitializationPhase.NotStarted)
                throw new InvalidOperationException("Initialisation has already begun.");
            DeleteState(); // Se.initialise deletes state before makeHeadNode does it again.
            state.Seed = seed ?? throw new ArgumentNullException(nameof(seed));
            state.StartNode = startNode;
            state.TargetRadius = config.GenRadius;
            state.TargetMargin = config.GenRadius - config.GenMinRadius;
            state.TargetRadiusMin = state.TargetRadius - state.TargetMargin;
            state.TargetRadiusMax = state.TargetRadius + state.TargetMargin;
            state.RestartCount = 0;
            state.GenerationCount = 0;
            state.Random = new AleaRandom(seed); // window.alea, separate from constructor/local Z.
            MakeHeadNode();
        }

        public void RestartGeneration()
        {
            if (!IsComplete) throw new InvalidOperationException("Restart is only valid after a completed head.");
            state.RestartCount++;
            state.AccumulatedOverflow = 0;
            state.TargetOverflow = 0;
            state.AntiTargetRange = 0;
            state.InOverflow = false;
            state.GenerationCount++;
            MakeHeadNode();
        }

        public bool Advance(int budget)
        {
            if (budget < 1) throw new ArgumentOutOfRangeException(nameof(budget));
            if (phase == MidlineInitializationPhase.NotStarted)
                throw new InvalidOperationException("Call Begin before Advance.");
            while (budget-- > 0 && phase != MidlineInitializationPhase.Complete)
                Step();
            return IsComplete;
        }

        void DeleteState()
        {
            // Dev L01307: old head survives; only its successor is detached.
            if (state.Head != null)
            {
                CoarseNode next = state.Head.Next;
                state.Head.Next = null;
                beforeRetire?.Invoke();
                while (next != null)
                {
                    CoarseNode following = next.Next;
                    nodes.Release(next);
                    next = following;
                }
            }
            state.Tail = state.FineTail = state.ObserverNode = null;
            state.Generation++;
            state.SmoothWindowHead = null;
            state.Bridging = false;
            state.OnBridgeApproach = false;
            foreach (var tracker in state.Trackers) tracker.Node = null;
            int side = state.SpiralDirection < 0 ? 0 : 1;
            state.HeadLayers = new MidlineLayerState
                {PreviousSide = 1, CurrentSide = side, NextLayerSwitch = state.SpiralDirection};
            state.TailLayers = new MidlineLayerState
                {PreviousSide = 1, CurrentSide = side, NextLayerSwitch = state.SpiralDirection};
            NotifyHeadLayers();
            height.SetLayers(0, 0);
        }

        void MakeHeadNode()
        {
            DeleteState();
            state.StartPoseRandom = new AleaRandom(config.LocalStartSeed + state.GenerationCount);
            radiusOffset = 0;
            phase = MidlineInitializationPhase.ChooseCandidate;
        }

        void NotifyHeadLayers()
        {
            layerEventCount++;
            headLayerChanged?.Invoke(state.HeadLayers.NegativeX, state.HeadLayers.NonNegativeX);
        }

        double Sample(double x, double z)
        {
            double value = height.GetXZ(x, z);
            queryCount++;
            return value;
        }

        void Step()
        {
            switch (phase)
            {
                case MidlineInitializationPhase.ChooseCandidate:
                    ChooseCandidate();
                    break;
                case MidlineInitializationPhase.QueryElevation:
                    elevation = Sample(candidateX, candidateZ);
                    phase = !dedicatedStart &&
                            (elevation < config.MinStartElevation || elevation > config.MaxStartElevation)
                        ? MidlineInitializationPhase.ChooseCandidate
                        : MidlineInitializationPhase.QueryMinusZ;
                    if (phase == MidlineInitializationPhase.ChooseCandidate) state.RestartCount++;
                    break;
                case MidlineInitializationPhase.QueryMinusZ:
                    minusZ = Sample(candidateX, candidateZ - 10);
                    phase = MidlineInitializationPhase.QueryPlusX;
                    break;
                case MidlineInitializationPhase.QueryPlusX:
                    plusX = Sample(candidateX + 10, candidateZ);
                    phase = MidlineInitializationPhase.QueryPlusZ;
                    break;
                case MidlineInitializationPhase.QueryPlusZ:
                    plusZ = Sample(candidateX, candidateZ + 10);
                    phase = MidlineInitializationPhase.ResolveHeading;
                    break;
                case MidlineInitializationPhase.ResolveHeading:
                    ResolveHeading();
                    break;
                case MidlineInitializationPhase.CommitHead:
                    CommitHead();
                    phase = MidlineInitializationPhase.Complete;
                    break;
                default:
                    throw new InvalidOperationException("Invalid initialisation phase " + phase);
            }
        }

        void ChooseCandidate()
        {
            dedicatedStart = config.StartPosition.HasValue && state.RestartCount < 3;
            if (dedicatedStart)
            {
                var start = config.StartPosition.Value;
                candidateX = start.X;
                candidateZ = start.Z;
            }
            else if (config.Spiralise)
            {
                if (state.RestartCount > 20)
                {
                    state.TailLayers.NegativeX++;
                    state.TailLayers.NonNegativeX++;
                    height.SetLayers(state.TailLayers.NegativeX, state.TailLayers.NonNegativeX, true);
                    state.HeadLayers.NegativeX++;
                    state.HeadLayers.NonNegativeX++;
                    NotifyHeadLayers();
                    state.RestartCount = 0;
                    radiusOffset = 0;
                    return; // Original continues the same loop; a budget may yield here.
                }
                if (state.RestartCount > 0)
                    radiusOffset = state.RestartCount % 2 == 1
                        ? config.RestartMargin * Math.Ceiling(state.RestartCount / 2.0)
                        : config.RestartMargin * -(state.RestartCount / 2.0);
                candidateX = 0;
                candidateZ = state.TargetRadius + radiusOffset;
            }
            else
            {
                if (state.RestartCount > 20)
                {
                    radiusOffset += state.StartPoseRandom.Next() * config.RestartMargin;
                    state.RestartCount = 0;
                }
                else radiusOffset = state.StartPoseRandom.Next() * config.RestartMargin;
                double angle = state.StartPoseRandom.Next() * Math.PI * 2;
                candidateX = MidlineTrig.Cos(angle) * radiusOffset;
                candidateZ = MidlineTrig.Sin(angle) * radiusOffset;
            }
            phase = MidlineInitializationPhase.QueryElevation;
        }

        void ResolveHeading()
        {
            if (config.StartHeading.HasValue && state.RestartCount < 3)
            {
                var heading = config.StartHeading.Value;
                state.HeadingVector = new Double3(heading.X, 0, heading.Z).Normalize();
            }
            else if (plusX - minusZ == 0 || plusZ - plusX == 0)
                state.HeadingVector = new Double3(0.99, 0, 0.01);
            else
            {
                double b = (elevation - minusZ) / (plusX - minusZ);
                double v = (elevation - plusX) / (plusZ - plusX);
                if (b >= 0 && b <= 1)
                    state.HeadingVector = new Double3(b, 0, -(1 - b)).Normalize();
                else if (v >= 0 && v <= 1)
                    state.HeadingVector = new Double3(1 - v, 0, v).Normalize();
                else
                {
                    state.RestartCount++;
                    phase = MidlineInitializationPhase.ChooseCandidate;
                    return;
                }
            }
            phase = MidlineInitializationPhase.CommitHead;
        }

        void CommitHead()
        {
            var heading = state.HeadingVector;
            var position = new Double3(candidateX, elevation - config.RoadSink, candidateZ);
            double angle = MidlineTrig.Atan2(heading.Z, heading.X);
            CoarseNode head = state.Head ?? nodes.Acquire(state.Generation);
            nodes.Unregister(head); // A restart can reuse an active head with a different logical index.
            head.Generation = state.Generation;
            head.Index = -17;
            nodes.Register(head);
            head.Position = position;
            head.Normal = new Double3(-heading.Z, 0, heading.X);
            head.Heading = angle;
            head.HeadingDelta = head.Turn = head.Grade = head.GradeForwardAverage = head.HeightCorrection = 0;
            head.Previous = head.Next = null;
            // B7.makeHeadNode writes blend=0 after the base method, including reuse.
            head.HasBlend = true;
            head.Blend = 0;
            state.Head = state.Tail = state.FineTail = state.ObserverNode = head;
            state.ObserverIndex = head.Index;
            if (config.Spiralise)
            {
                Double3 dir = position.Negate().Normalize();
                state.TargetDirection = new Double3(-dir.Z * state.SpiralDirection, 0,
                    dir.X * state.SpiralDirection).Normalize();
            }
            else if (config.Direction.HasValue)
            {
                Double3 dir = config.Direction.Value;
                state.TargetDirection = new Double3(dir.X, state.TargetDirection.Y, dir.Z).Normalize();
            }
            else
                state.TargetDirection = new Double3(-position.X, 0, -position.Z).Normalize();
            state.MidlineDirection = new Double3(MidlineTrig.Cos(angle), 0, MidlineTrig.Sin(angle));
            state.MidlineOrthogonal = new Double3(-state.MidlineDirection.Z, 0, state.MidlineDirection.X);
            state.PreviousTargetSide = Math.Sign(state.MidlineOrthogonal.Dot(state.TargetDirection) * -1);
            state.SmoothWindowHead = head;
            foreach (var tracker in state.Trackers)
            {
                tracker.Node = head;
                tracker.Delta = 0;
            }
        }
    }
}
