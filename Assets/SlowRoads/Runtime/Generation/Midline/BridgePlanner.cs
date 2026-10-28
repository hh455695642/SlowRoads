using System;

namespace SlowRoads.Generation.Midline
{
    public sealed class BridgeCandidate
    {
        public CoarseNode SpawnNode;
        public Double3 Landing;
        public double Distance, Quality;
        public bool HasHeight;
        public double Height;
        public Double3 OptionA, OptionB, OptionC;

        // Dev L01959 does not clear height; its presence is meaningful later.
        public void Reset()
        {
            SpawnNode = null;
            Distance = Quality = 0;
            Landing = new Double3();
        }
    }

    public enum BridgeSearchPhase
    {
        NotStarted, InitialLanding, EmergencyLanding, SearchLanding,
        QualityMidpoint, QualityRight, QualityLeft, QualityHalfRight,
        QualityHalfLeft, Commit, Complete
    }

    /// <summary>
    /// Resumable Se.testForBridge/checkBridgeQuality, Dev L01992–L02094.
    /// One Advance unit is at most one height query or one state transition;
    /// emergency search can yield after every query without changing the result.
    /// </summary>
    public sealed class BridgePlanner
    {
        readonly MidlineConfig config;
        readonly IMidlineHeightSource height;
        readonly BridgeCandidate candidate;
        readonly double maximumDistance, minimumDistance;
        BridgeSearchPhase phase;
        CoarseNode spawn;
        Double3 direction, targetDirection, antiBridgePoint;
        Double3 landing, midpoint, perpendicular;
        double sign, targetDot, tailIndex;
        bool allowEmergency, demoMode, fellShort;
        double lower, upper, probe, probeCount, minimumLandingElevation;
        int probeOrdinal;
        double quality, distanceTerm, elevationTerm, waterTerm, targetTerm;
        double rightScore, leftScore;
        int queryCount;

        public BridgePlanner(MidlineConfig config, IMidlineHeightSource height, BridgeCandidate candidate)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.height = height ?? throw new ArgumentNullException(nameof(height));
            this.candidate = candidate ?? throw new ArgumentNullException(nameof(candidate));
            maximumDistance = config.MaximumBridgeNodes * 10;
            minimumDistance = config.MinimumBridgeNodes * 10;
        }

        public BridgeSearchPhase Phase => phase;
        public bool IsComplete => phase == BridgeSearchPhase.Complete;
        public Double3 ScratchLanding => landing;
        public int QueryCount => queryCount;
        public double LastQuality => quality;

        // Dev L01965–L01991. The candidate's landing stays unchanged while its
        // spawn pointer walks back at most ten nodes.
        public static void ImproveStartNode(BridgeCandidate candidate)
        {
            if (candidate?.SpawnNode == null || candidate.SpawnNode.Previous == null)
                throw new InvalidOperationException("Bridge improvement requires a predecessor.");
            CoarseNode node = candidate.SpawnNode;
            double closest = DistanceSquared(node.Position, candidate.Landing);
            double nearbyLimit = closest + 225;
            double bestAlignment = Alignment(node, candidate.Landing);
            node = node.Previous;
            for (int i = 0; i < 10 && node != null &&
                node.Previous != null && node.Position.Y <= candidate.Landing.Y + 4; i++)
            {
                double distance = DistanceSquared(node.Position, candidate.Landing);
                double alignment = Alignment(node, candidate.Landing);
                if (distance < closest) { closest = distance; candidate.SpawnNode = node; }
                if (alignment > bestAlignment && (alignment > 0.707 || distance < nearbyLimit))
                { bestAlignment = alignment; candidate.SpawnNode = node; }
                if (alignment < 0.707) candidate.SpawnNode = node;
                node = node.Previous;
            }
        }

        static double DistanceSquared(Double3 a, Double3 b)
        {
            double x = a.X - b.X, y = a.Y - b.Y, z = a.Z - b.Z;
            return x * x + y * y + z * z;
        }

        static double Alignment(CoarseNode node, Double3 landing)
        {
            double gx = node.Position.X - node.Previous.Position.X;
            double gz = node.Position.Z - node.Previous.Position.Z;
            double gl = Math.Sqrt(gx * gx + gz * gz);
            if (gl != 0) { gx /= gl; gz /= gl; }
            double cx = landing.X - node.Position.X;
            double cz = landing.Z - node.Position.Z;
            double cl = Math.Sqrt(cx * cx + cz * cz);
            if (cl != 0) { cx /= cl; cz /= cl; }
            return gx * cx + gz * cz;
        }

        public void Begin(CoarseNode spawn, double sign, Double3 direction,
            Double3 targetDirection, double targetDot, Double3 antiBridgePoint,
            bool allowEmergency, int tailIndex, bool demoMode = false)
        {
            if (phase != BridgeSearchPhase.NotStarted && !IsComplete)
                throw new InvalidOperationException("Finish the previous bridge search first.");
            this.spawn = spawn ?? throw new ArgumentNullException(nameof(spawn));
            this.sign = sign;
            this.direction = direction;
            this.targetDirection = targetDirection;
            this.targetDot = targetDot;
            this.antiBridgePoint = antiBridgePoint;
            this.allowEmergency = allowEmergency;
            this.demoMode = demoMode;
            this.tailIndex = tailIndex;
            lower = 0;
            upper = maximumDistance;
            probeCount = 5;
            probe = upper / 2;
            fellShort = false;
            probeOrdinal = 0;
            minimumLandingElevation = spawn.Position.Y - 2;
            queryCount = 0;
            quality = 0;
            landing = new Double3();
            phase = BridgeSearchPhase.InitialLanding;
        }

        public bool Advance(int budget)
        {
            if (budget < 1) throw new ArgumentOutOfRangeException(nameof(budget));
            if (phase == BridgeSearchPhase.NotStarted)
                throw new InvalidOperationException("Call Begin before Advance.");
            while (budget-- > 0 && !IsComplete) Step();
            return IsComplete;
        }

        double Sample(double x, double z)
        {
            double result = height.GetXZ(x, z);
            queryCount++;
            return result;
        }

        void SampleLanding(double distance)
        {
            double x = spawn.Position.X + direction.X * distance * sign;
            double z = spawn.Position.Z + direction.Z * distance * sign;
            landing = new Double3(x, Sample(x, z), z);
        }

        static double Distance(Double3 a, Double3 b)
        {
            double x = a.X - b.X, y = a.Y - b.Y, z = a.Z - b.Z;
            return Math.Sqrt(x * x + y * y + z * z);
        }

        void Step()
        {
            switch (phase)
            {
                case BridgeSearchPhase.InitialLanding:
                    SampleLanding(upper);
                    if (landing.Y < minimumLandingElevation)
                    {
                        landing = landing.WithY(0);
                        lower = maximumDistance;
                        fellShort = true;
                    }
                    if (allowEmergency && targetDot < 0 && !demoMode)
                    {
                        fellShort = false;
                        phase = BridgeSearchPhase.EmergencyLanding;
                    }
                    else phase = BridgeSearchPhase.SearchLanding;
                    break;
                case BridgeSearchPhase.EmergencyLanding:
                    SampleLanding(upper);
                    upper += maximumDistance / 2;
                    probeCount += 0.51;
                    if (landing.Y >= minimumLandingElevation) phase = BridgeSearchPhase.SearchLanding;
                    break;
                case BridgeSearchPhase.SearchLanding:
                    if (probeOrdinal >= probeCount)
                    {
                        phase = probe < minimumDistance || Distance(landing, antiBridgePoint) < 100
                            ? BridgeSearchPhase.Complete : BridgeSearchPhase.QualityMidpoint;
                        break;
                    }
                    SampleLanding(probe);
                    if (landing.Y < minimumLandingElevation)
                    {
                        if (fellShort) { phase = BridgeSearchPhase.Complete; break; }
                        lower = probe;
                        probe = lower * 0.5 + upper * 0.5;
                    }
                    else
                    {
                        upper = probe;
                        probe = lower * 0.5 + upper * 0.5;
                    }
                    fellShort = false;
                    probeOrdinal++;
                    break;
                case BridgeSearchPhase.QualityMidpoint:
                    midpoint = new Double3(
                        (spawn.Position.X + landing.X) * 0.5,
                        (spawn.Position.Y + landing.Y) * 0.5,
                        (spawn.Position.Z + landing.Z) * 0.5);
                    distanceTerm = 1 - (probe - minimumDistance) / maximumDistance;
                    elevationTerm = 1 - Math.Min(1, Math.Abs(spawn.Position.Y - landing.Y) / 4);
                    waterTerm = Sample(midpoint.X, midpoint.Z);
                    if (waterTerm > 0) { quality = -1; phase = BridgeSearchPhase.Complete; break; }
                    waterTerm = waterTerm > -2 ? waterTerm / -2 : 1;
                    var bridgeDirection = new Double3(
                        landing.X - spawn.Position.X, 0, landing.Z - spawn.Position.Z).Normalize();
                    targetTerm = (bridgeDirection.Dot(targetDirection) + 1) / 2;
                    perpendicular = new Double3(-bridgeDirection.Z, 0, bridgeDirection.X);
                    phase = BridgeSearchPhase.QualityRight;
                    break;
                case BridgeSearchPhase.QualityRight:
                    rightScore = Sample(perpendicular.X * probe + midpoint.X,
                        perpendicular.Z * probe + midpoint.Z);
                    if (rightScore > 0) { quality = -1; phase = BridgeSearchPhase.Complete; break; }
                    rightScore = rightScore > -4 ? rightScore / -4 : 1;
                    phase = BridgeSearchPhase.QualityLeft;
                    break;
                case BridgeSearchPhase.QualityLeft:
                    leftScore = Sample(perpendicular.X * -probe + midpoint.X,
                        perpendicular.Z * -probe + midpoint.Z);
                    if (leftScore > 0) { quality = -1; phase = BridgeSearchPhase.Complete; break; }
                    leftScore = leftScore > -4 ? leftScore / -4 : 1;
                    phase = BridgeSearchPhase.QualityHalfRight;
                    break;
                case BridgeSearchPhase.QualityHalfRight:
                    if (Sample(perpendicular.X * (probe / 2) + midpoint.X,
                        perpendicular.Z * (probe / 2) + midpoint.Z) > 0)
                    {
                        quality = -1;
                        phase = BridgeSearchPhase.Complete;
                    }
                    else phase = BridgeSearchPhase.QualityHalfLeft;
                    break;
                case BridgeSearchPhase.QualityHalfLeft:
                    if (Sample(perpendicular.X * (-probe / 2) + midpoint.X,
                        perpendicular.Z * (-probe / 2) + midpoint.Z) > 0)
                    {
                        quality = -1;
                        phase = BridgeSearchPhase.Complete;
                    }
                    else
                    {
                        double side = (leftScore + rightScore) / 2;
                        quality = (distanceTerm + elevationTerm + waterTerm + targetTerm + side) / 5;
                        phase = BridgeSearchPhase.Commit;
                    }
                    break;
                case BridgeSearchPhase.Commit:
                    if (quality > 0 && (quality > candidate.Quality ||
                        (candidate.SpawnNode != null && candidate.SpawnNode.Index < tailIndex - 50)))
                    {
                        candidate.Quality = quality;
                        candidate.SpawnNode = spawn;
                        candidate.Distance = probe;
                        candidate.Landing = landing;
                    }
                    phase = BridgeSearchPhase.Complete;
                    break;
                default:
                    throw new InvalidOperationException("Invalid bridge phase " + phase);
            }
        }
    }

    public enum BridgeSweepPhase { NotStarted, OptionA, OptionB, OptionC, Complete }

    /// <summary>
    /// Ordered A/B/C direction assembly from Dev L01530–L01561. The mutable
    /// working direction intentionally carries B's value into C, even when B
    /// was skipped. Each candidate search keeps its own resumable PC.
    /// </summary>
    public sealed class BridgeCandidateSweep
    {
        readonly MidlineConfig config;
        readonly MidlineState state;
        readonly SteeringDraft draft;
        readonly BridgeCandidate candidate;
        readonly BridgePlanner planner;
        readonly Action<int, Double3> probeStarted;
        BridgeSweepPhase phase;
        Double3 workingDirection;
        bool searchActive;
        double bridgeTargetSide;

        public BridgeCandidateSweep(MidlineConfig config, MidlineState state,
            SteeringDraft draft, IMidlineHeightSource height,
            BridgeCandidate candidate, Action<int, Double3> probeStarted = null)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.draft = draft ?? throw new ArgumentNullException(nameof(draft));
            this.candidate = candidate ?? throw new ArgumentNullException(nameof(candidate));
            planner = new BridgePlanner(config, height, candidate);
            this.probeStarted = probeStarted;
            phase = BridgeSweepPhase.NotStarted;
        }

        public BridgeSweepPhase Phase => phase;
        public bool IsComplete => phase == BridgeSweepPhase.Complete;

        public void Begin()
        {
            if (phase != BridgeSweepPhase.NotStarted)
                throw new InvalidOperationException("Candidate sweep already begun.");
            if (!draft.NeedsBridgeCandidate || state.Tail?.Previous == null)
                throw new InvalidOperationException("Source bridge condition or spawn predecessor missing.");
            bridgeTargetSide = Math.Sign(draft.LateralGrade) == draft.TargetSide ? -1 : 1;
            phase = BridgeSweepPhase.OptionA;
        }

        public bool Advance(int budget)
        {
            if (budget < 1) throw new ArgumentOutOfRangeException(nameof(budget));
            if (phase == BridgeSweepPhase.NotStarted)
                throw new InvalidOperationException("Call Begin before Advance.");
            while (budget-- > 0 && !IsComplete)
            {
                if (searchActive)
                {
                    if (planner.Advance(1))
                    {
                        switch (phase)
                        {
                            case BridgeSweepPhase.OptionA: candidate.OptionA = planner.ScratchLanding; break;
                            case BridgeSweepPhase.OptionB: candidate.OptionB = planner.ScratchLanding; break;
                            case BridgeSweepPhase.OptionC: candidate.OptionC = planner.ScratchLanding; break;
                        }
                        searchActive = false;
                        NextOption();
                    }
                    continue;
                }
                switch (phase)
                {
                    case BridgeSweepPhase.OptionA:
                        workingDirection = new Double3();
                        if (state.TargetOverflow <= 0)
                        {
                            workingDirection = state.MidlineDirection.Normalize();
                            BeginProbe(0);
                        }
                        else { candidate.OptionA = new Double3(); NextOption(); }
                        break;
                    case BridgeSweepPhase.OptionB:
                        double side = Math.Sign(draft.LateralGrade);
                        var normal = state.Tail.Previous.Normal;
                        workingDirection = new Double3(normal.X * side, 0, normal.Z * side).Normalize();
                        if (config.TestBridgeLateral && draft.TargetDot + bridgeTargetSide > -1)
                        {
                            workingDirection = new Double3(
                                workingDirection.X + state.MidlineDirection.X * 0.15, 0,
                                workingDirection.Z + state.MidlineDirection.Z * 0.15).Normalize();
                            BeginProbe(1);
                        }
                        else { candidate.OptionB = new Double3(); NextOption(); }
                        break;
                    case BridgeSweepPhase.OptionC:
                        if (draft.TargetDot + 0.7071 * bridgeTargetSide > -1)
                        {
                            workingDirection = new Double3(
                                workingDirection.X + state.MidlineDirection.X, 0,
                                workingDirection.Z + state.MidlineDirection.Z).Normalize();
                            BeginProbe(2);
                        }
                        else { candidate.OptionC = new Double3(); NextOption(); }
                        break;
                    default: throw new InvalidOperationException("Invalid bridge sweep phase " + phase);
                }
            }
            return IsComplete;
        }

        void BeginProbe(int option)
        {
            probeStarted?.Invoke(option, workingDirection);
            planner.Begin(state.Tail.Previous, 1, workingDirection,
                state.TargetDirection, draft.TargetDot, state.AntiBridgePoint,
                state.AllowEmergencyBridge, state.Tail.Index);
            searchActive = true;
        }

        void NextOption()
        {
            switch (phase)
            {
                case BridgeSweepPhase.OptionA: phase = BridgeSweepPhase.OptionB; break;
                case BridgeSweepPhase.OptionB: phase = BridgeSweepPhase.OptionC; break;
                case BridgeSweepPhase.OptionC: phase = BridgeSweepPhase.Complete; break;
            }
        }
    }
}
