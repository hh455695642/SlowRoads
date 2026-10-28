using System;

namespace SlowRoads.Generation.Midline
{
    /// <summary>Observed T/E values of one Se steering step before tracker and bridge resolution.</summary>
    public sealed class SteeringDraft
    {
        public double FeelDistance, FeelLeft, FeelRight, FeelAhead, FeelAverage, FeelDifference;
        public double LateralGrade, LongitudinalGrade, LeftGrade, RightGrade, GradeAverage;
        public double MinimumTurn, MaximumTurn, MaximumDelta, ProposedTurn, FinalTurn;
        public double TargetDot, TargetSide, TargetAngle, TargetAdjust, TargetRedirect;
        public double Direction, NextX, NextZ, RoadSinkFactor;
        public bool NeedsBridgeCandidate, ViolatedOverflow;
        // Dev L01852 writes a misspelled E field; an undefined *= value becomes NaN.
        public bool HasMisspelledRoadSinkFactor;
        public double MisspelledRoadSinkFactor;
    }

    /// <summary>
    /// Se.extendMidline arithmetic from Dev L01422–L01529, L01562–L01596 and
    /// L01846–L01877. A draft stops before bridge/tracker/rollback choices; it
    /// cannot be published as a coarse node until those original branches run.
    /// </summary>
    public static class MidlineSteering
    {
        public static SteeringDraft Prepare(MidlineConfig config, MidlineState state,
            IMidlineHeightSource height, SteeringDraft reusable = null, bool deferCompletion = false)
        {
            if (config == null || state == null || height == null) throw new ArgumentNullException();
            CoarseNode tail = state.Tail ?? throw new InvalidOperationException("Head not initialised.");
            var draft = reusable ?? new SteeringDraft();
            draft.HasMisspelledRoadSinkFactor = false;
            draft.FeelDistance = config.FeelDistance * (1 + state.WaterFactor);
            // The archived getMidlineXZ ignores the third argument passed by Se.
            draft.FeelLeft = height.GetXZ(
                MidlineTrig.Cos(tail.Heading - config.FeelAngle) * draft.FeelDistance + tail.Position.X,
                MidlineTrig.Sin(tail.Heading - config.FeelAngle) * draft.FeelDistance + tail.Position.Z);
            draft.FeelRight = height.GetXZ(
                MidlineTrig.Cos(tail.Heading + config.FeelAngle) * draft.FeelDistance + tail.Position.X,
                MidlineTrig.Sin(tail.Heading + config.FeelAngle) * draft.FeelDistance + tail.Position.Z);
            draft.FeelAhead = height.GetXZ(tail.Position.X, tail.Position.Z);
            draft.FeelAhead = Math.Max(draft.FeelAhead, config.MinimumElevation);
            state.WaterFactor = 0;
            state.ByWater = false;
            if (draft.FeelRight < config.MinimumElevation * 2) state.ByWater = true;
            else if (draft.FeelLeft < config.MinimumElevation * 2) state.ByWater = true;
            draft.FeelAverage = (draft.FeelRight + draft.FeelLeft) / 2;
            if (draft.FeelAverage < config.MinimumElevation * 3)
                state.WaterFactor = draft.FeelAverage > config.MinimumElevation
                    ? 1 - (draft.FeelAverage - config.MinimumElevation) /
                        (config.MinimumElevation * 2)
                    : 1;
            draft.FeelDifference = (draft.FeelRight - draft.FeelLeft) / 2;

            double allowableGrade = config.MaxGrad + state.AntiWeight * config.MaxGrad * 0.5;
            draft.LateralGrade = (draft.FeelLeft - draft.FeelRight) / draft.FeelDistance;
            draft.LateralGrade /= allowableGrade;
            draft.LongitudinalGrade = (draft.FeelAverage - draft.FeelAhead) / draft.FeelDistance;
            draft.LongitudinalGrade /= allowableGrade;
            draft.ProposedTurn = 0;
            if (draft.FeelDifference != 0)
                draft.ProposedTurn = (draft.FeelAverage - draft.FeelAhead) / draft.FeelDifference;
            draft.ProposedTurn = Math.Max(Math.Min(draft.ProposedTurn, 1), -1);
            state.MidlineDirection = new Double3(MidlineTrig.Cos(tail.Heading), 0, MidlineTrig.Sin(tail.Heading));
            state.MidlineOrthogonal = new Double3(-state.MidlineDirection.Z, 0, state.MidlineDirection.X);
            draft.TargetDot = Math.Max(Math.Min(1, state.MidlineDirection.Dot(state.TargetDirection)), -1);
            draft.TargetSide = Math.Sign(state.MidlineOrthogonal.Dot(state.TargetDirection) * -1);
            if (draft.TargetSide == 0) draft.TargetSide = state.PreviousTargetSide;
            if (draft.TargetSide == state.PreviousTargetSide && state.TargetOverflow > 1.5)
                draft.TargetSide = draft.TargetSide == 1 ? -2 : 2;
            if ((draft.TargetDot < 0 || state.TargetOverflow > 0) &&
                draft.TargetSide != state.PreviousTargetSide)
            {
                if (draft.TargetSide > state.PreviousTargetSide)
                {
                    if (state.PreviousTurn < 0)
                    {
                        state.TargetOverflow += state.PreviousTurn;
                        if (state.InOverflow && state.TargetOverflow <= 0) state.TargetOverflow = 0.01;
                        if (state.TargetOverflow > 0)
                        {
                            draft.TargetSide = state.PreviousTargetSide;
                            draft.TargetDot = -1;
                        }
                        else state.TargetOverflow = 0;
                    }
                    else
                    {
                        state.TargetOverflow += state.PreviousTurn;
                        draft.TargetSide = state.PreviousTargetSide;
                        draft.TargetDot = -1;
                    }
                }
                else
                {
                    if (state.PreviousTurn > 0)
                    {
                        state.TargetOverflow -= state.PreviousTurn;
                        if (state.InOverflow && state.TargetOverflow <= 0) state.TargetOverflow = 0.01;
                        if (state.TargetOverflow > 0)
                        {
                            draft.TargetSide = state.PreviousTargetSide;
                            draft.TargetDot = -1;
                        }
                        else state.TargetOverflow = 0;
                    }
                    else
                    {
                        state.TargetOverflow -= state.PreviousTurn;
                        draft.TargetSide = state.PreviousTargetSide;
                        draft.TargetDot = -1;
                    }
                }
                state.AccumulatedOverflow += state.TargetOverflow;
            }
            else
            {
                state.TargetOverflow = 0;
                if (state.AccumulatedOverflow > 0)
                {
                    state.AccumulatedOverflow -= 3;
                    if (state.AccumulatedOverflow < 0) state.AccumulatedOverflow = 0;
                }
            }
            state.InOverflow = state.TargetOverflow > 0;
            state.PreviousTargetSide = draft.TargetSide;
            draft.TargetAngle = MidlineTrig.Acos(draft.TargetDot);
            draft.LeftGrade = (draft.FeelLeft - draft.FeelAhead) / draft.FeelDistance / allowableGrade;
            draft.RightGrade = (draft.FeelRight - draft.FeelAhead) / draft.FeelDistance / allowableGrade;
            draft.MaximumTurn = 1;
            draft.MinimumTurn = -1;
            double leftAbs = Math.Abs(draft.LeftGrade), rightAbs = Math.Abs(draft.RightGrade);
            if (leftAbs > 1 || rightAbs > 1)
            {
                double gradeDifference = (draft.LeftGrade - draft.RightGrade) / 2;
                double gradeAverage = (draft.LeftGrade + draft.RightGrade) / 2;
                if ((draft.LeftGrade > 1 && draft.RightGrade > 1) ||
                    (draft.LeftGrade < -1 && draft.RightGrade < -1))
                {
                    if (leftAbs < rightAbs) draft.MaximumTurn = draft.MinimumTurn = 1;
                    else draft.MaximumTurn = draft.MinimumTurn = -1;
                }
                else
                {
                    if (leftAbs > 1)
                        draft.MaximumTurn = draft.LeftGrade < 0
                            ? (1 + gradeAverage) / Math.Abs(gradeDifference)
                            : (1 - gradeAverage) / Math.Abs(gradeDifference);
                    if (rightAbs > 1)
                    {
                        draft.MinimumTurn = draft.RightGrade < 0
                            ? (1 + gradeAverage) / Math.Abs(gradeDifference)
                            : (1 - gradeAverage) / Math.Abs(gradeDifference);
                        draft.MinimumTurn *= -1;
                    }
                }
            }
            draft.GradeAverage = (Math.Abs(draft.LateralGrade) + Math.Abs(draft.LongitudinalGrade)) / 2;
            draft.MaximumDelta = config.MaxTurnDelta + Math.Abs(draft.GradeAverage) * 0.5;
            draft.MaximumTurn *= 1 + state.AntiWeight * 0.25;
            draft.MinimumTurn *= 1 + state.AntiWeight * 0.25;
            draft.MaximumTurn = draft.MaximumTurn > state.PreviousTurn
                ? Math.Min(draft.MaximumTurn, state.PreviousTurn + draft.MaximumDelta)
                : Math.Max(draft.MaximumTurn, state.PreviousTurn - draft.MaximumDelta);
            draft.MinimumTurn = draft.MinimumTurn < state.PreviousTurn
                ? Math.Max(draft.MinimumTurn, state.PreviousTurn - draft.MaximumDelta)
                : Math.Min(draft.MinimumTurn, state.PreviousTurn + draft.MaximumDelta);
            if (state.ByWater)
            {
                if (draft.LateralGrade < 0)
                {
                    if (draft.MaximumTurn > 0) draft.MaximumTurn *= 1 - state.WaterFactor;
                }
                else if (draft.MinimumTurn < 0) draft.MinimumTurn *= 1 - state.WaterFactor;
            }
            draft.ProposedTurn = Math.Max(Math.Min(draft.MaximumTurn, draft.ProposedTurn), draft.MinimumTurn);
            draft.NeedsBridgeCandidate = tail.Index > 1 && !state.Bridging &&
                draft.FeelAhead < config.MaximumBridgeElevation &&
                draft.FeelAhead > config.MinimumBridgeElevation;
            if (!deferCompletion) FinishPreparation(config, state, draft);
            return draft;
        }

        // Original bridge queries occur before AntiTarget can clear emergency permission.
        public static void FinishPreparation(MidlineConfig config, MidlineState state, SteeringDraft draft)
        {
            CoarseNode tail = state.Tail;
            double longitudinalSquared = draft.LongitudinalGrade * draft.LongitudinalGrade;
            if (longitudinalSquared <= 1)
            {
                if (!state.ByWater || draft.LongitudinalGrade > 0)
                    draft.ProposedTurn *= longitudinalSquared;
            }
            else draft.ProposedTurn = draft.ProposedTurn < 0 ? draft.MinimumTurn : draft.MaximumTurn;
            draft.ProposedTurn = Math.Max(Math.Min(draft.MaximumTurn, draft.ProposedTurn), draft.MinimumTurn);
            state.AntiWeight = 0;
            if (state.AntiTargetRange > 0)
            {
                double dx = tail.Position.X - state.AntiTarget.X;
                double distanceSquared = dx * dx +
                    (tail.Position.Z - state.AntiTarget.Z) * (tail.Position.Z - state.AntiTarget.Z);
                state.AntiDirection = new Double3(state.AntiTarget.X - tail.Position.X, 0,
                    state.AntiTarget.Z - tail.Position.Z).Normalize();
                double antiDot = state.MidlineDirection.Dot(state.AntiDirection);
                if (distanceSquared < state.AntiTargetRange)
                {
                    state.AntiWeight = 1 - distanceSquared / state.AntiTargetRange;
                    double antiSide = state.MidlineOrthogonal.Dot(state.AntiDirection);
                    double antiRedirect = config.FeelAngle * 2 * antiSide;
                    double antiAdjust = (antiDot + 1) / 2;
                    state.AntiWeight *= antiAdjust;
                    antiRedirect = Math.Max(Math.Min(draft.MaximumTurn, antiRedirect), draft.MinimumTurn);
                    draft.ProposedTurn = state.AntiWeight * antiRedirect +
                        (1 - state.AntiWeight) * draft.ProposedTurn;
                }
                else if (antiDot < -0.707)
                {
                    state.AntiTargetRange = 0;
                    state.RevertTrackerIndex = null;
                    state.TrackRevertCount = 0;
                    state.AntiTargetTriggerCount = 0;
                    state.AllowEmergencyBridge = false;
                }
            }
            draft.TargetAdjust = (1 - (draft.TargetDot + 1) / 2) *
                (1 - state.WaterFactor * state.WaterFactor);
            draft.TargetAdjust *= 1 - state.AntiWeight;
            draft.TargetRedirect = draft.TargetAngle * draft.TargetSide / config.FeelAngle;
            draft.TargetRedirect = Math.Max(Math.Min(draft.MaximumTurn, draft.TargetRedirect), draft.MinimumTurn);
            draft.ProposedTurn = draft.TargetAdjust * draft.TargetRedirect +
                (1 - draft.TargetAdjust) * draft.ProposedTurn;
            draft.ViolatedOverflow = (state.AccumulatedOverflow > 150 ||
                state.TargetOverflow > config.MaximumTargetOverflow) && !state.Bridging;
        }

        public static CoarseNode PredictOrdinaryNode(MidlineConfig config, MidlineState state,
            IMidlineHeightSource height, SteeringDraft draft,
            CoarseNode destination = null, bool bridgeResolved = false)
        {
            if ((draft.NeedsBridgeCandidate || state.Bridging) && !bridgeResolved)
                throw new InvalidOperationException("BridgePlanner must resolve this step first.");
            if (draft.ViolatedOverflow && !config.IsStraight)
                throw new InvalidOperationException("Rollback must resolve overflow before node creation.");
            CoarseNode tail = state.Tail;
            draft.FinalTurn = config.IsStraight ? 0 : state.Bridging ? draft.ProposedTurn :
                (1 - state.AntiWeight) * (draft.ProposedTurn * config.SmoothFactor) +
                state.AntiWeight * draft.ProposedTurn;
            draft.Direction = tail.Heading - draft.FinalTurn * config.FeelAngle;
            if (draft.Direction < -Math.PI) draft.Direction += Math.PI * 2;
            else if (draft.Direction > Math.PI) draft.Direction -= Math.PI * 2;
            draft.RoadSinkFactor = config.RoadSink;
            if (draft.FeelAverage < 10) draft.RoadSinkFactor = 0;
            else if (draft.FeelAverage < 20)
            {
                draft.HasMisspelledRoadSinkFactor = true;
                draft.MisspelledRoadSinkFactor = double.NaN; // undefined *= value in Dev L01852
            }
            draft.NextX = MidlineTrig.Cos(draft.Direction) * config.NodeSpacing + tail.Position.X;
            draft.NextZ = MidlineTrig.Sin(draft.Direction) * config.NodeSpacing + tail.Position.Z;
            double sampled = height.GetXZ(draft.NextX, draft.NextZ);
            var node = destination ?? new CoarseNode();
            node.Index = tail.Index + 1;
            node.Position = new Double3(draft.NextX,
                Math.Max(sampled, config.MinimumElevation) - draft.RoadSinkFactor, draft.NextZ);
            node.Heading = draft.Direction;
            node.HeadingDelta = draft.FinalTurn * config.FeelAngle;
            node.Turn = draft.FinalTurn;
            node.HeightCorrection = 0;
            node.Grade = draft.LateralGrade;
            node.GradeFiltered = draft.LongitudinalGrade;
            node.GradeForwardAverage = Math.Abs(draft.LongitudinalGrade);
            node.Normal = new Double3(0, 0, 1);
            node.HasTargetDot = true;
            node.TargetDot = draft.TargetDot;
            return node;
        }
    }
}
