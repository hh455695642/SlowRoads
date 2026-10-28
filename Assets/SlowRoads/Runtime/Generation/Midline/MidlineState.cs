using SlowRoads.Generation.Random;

namespace SlowRoads.Generation.Midline
{
    public sealed class MidlineLayerState
    {
        public int PreviousSide = 1, CurrentSide = 1, NextLayerSwitch = 1;
        public int NegativeX, NonNegativeX;
        public int PreviousSwitchNode = -100;
        public bool HasPreviousSwitchNode;
    }

    public sealed class MidlineTrackerState
    {
        public int Index, Window, Radius;
        public double Limit, Offset, ToleranceFactor = 1, Tolerance, DeltaThreshold = 3.3, Delta;
        public double? GradeThreshold;
        public double TargetDot;
        public CoarseNode Node;
    }

    /// <summary>Live Se fields for B02. Source fields not read until later batches remain in their owners.</summary>
    public sealed class MidlineState
    {
        public int Generation { get; internal set; }
        public int GenerationCount { get; internal set; }
        public int RestartCount { get; internal set; }
        public int StartNode { get; internal set; }
        public string Seed { get; internal set; }
        public int SpiralDirection { get; internal set; }
        public double TargetRadius { get; internal set; }
        public double TargetMargin { get; internal set; }
        public double TargetRadiusMin { get; internal set; }
        public double TargetRadiusMax { get; internal set; }
        public Double3 TargetDirection { get; internal set; }
        public Double3 MidlineDirection { get; internal set; }
        public Double3 MidlineOrthogonal { get; internal set; }
        public Double3 HeadingVector { get; internal set; }
        public double PreviousTargetSide { get; internal set; }
        public double PreviousTurn { get; internal set; }
        public double WaterFactor { get; internal set; }
        public bool ByWater { get; internal set; }
        public double TargetOverflow { get; internal set; }
        public double AccumulatedOverflow { get; internal set; }
        public bool InOverflow { get; internal set; }
        public double AntiWeight { get; internal set; }
        public Double3 AntiDirection { get; internal set; }
        public int MaxRetractIndex { get; internal set; }
        public double TotalHeadingDelta { get; internal set; }
        public bool Bridging { get; internal set; }
        public bool AllowEmergencyBridge { get; internal set; }
        public Double3 AntiBridgePoint { get; internal set; }
        public int? RevertTrackerIndex { get; internal set; }
        public int TrackRevertCount { get; internal set; }
        public int LastResetIndex { get; internal set; }
        public double AntiTargetRevert { get; internal set; } = 40;
        public Double3 AntiTargetTriggerPoint { get; internal set; }
        public int AntiTargetTrigger { get; internal set; }
        public bool OnBridgeApproach { get; internal set; }
        public bool OnBridgeDismount { get; internal set; }
        public double BridgeApproachFactor { get; internal set; }
        // E.maxRetract survives subsequent updates and generation restarts.
        public int MaxRetractCount { get; internal set; }
        public Double3 AntiTarget { get; internal set; }
        public double AntiTargetRange { get; internal set; }
        public int AntiTargetTriggerCount { get; internal set; }
        public CoarseNode Head { get; internal set; }
        public CoarseNode Tail { get; internal set; }
        public CoarseNode FineTail { get; internal set; }
        public CoarseNode SmoothWindowHead { get; internal set; }
        public double SmoothMidlineHeadHeight { get; internal set; }
        public CoarseNode ObserverNode { get; internal set; }
        public int ObserverIndex { get; internal set; }
        public MidlineLayerState HeadLayers { get; internal set; } = new MidlineLayerState();
        public MidlineLayerState TailLayers { get; internal set; } = new MidlineLayerState();
        public MidlineTrackerState[] Trackers { get; } = CreateTrackers();
        public AleaRandom ConstructorRandom { get; internal set; }
        public AleaRandom Random { get; internal set; }
        public AleaRandom StartPoseRandom { get; internal set; }

        // The B05 independent probe accepts a captured pre-branch coarse state.
        // The full session will own construction once B06 connects the modules.
        public static MidlineState ForBridgeProbe(CoarseNode tail, Double3 targetDirection,
            Double3 midlineDirection) => new MidlineState
        {
            Tail = tail,
            TargetDirection = targetDirection,
            MidlineDirection = midlineDirection,
        };

        static MidlineTrackerState[] CreateTrackers() => new[]
        {
            new MidlineTrackerState {Index=0,Window=15,Radius=15,Limit=3.3,Offset=0},
            new MidlineTrackerState {Index=1,Window=30,Radius=10,Limit=3.3,Offset=0.5,GradeThreshold=20},
            new MidlineTrackerState {Index=2,Window=60,Radius=15,Limit=3.4,Offset=0.75,GradeThreshold=25},
            new MidlineTrackerState {Index=3,Window=120,Radius=20,Limit=3.6,Offset=0.8},
        };
    }
}
