using System;

namespace SlowRoads.Generation.Midline
{
    public readonly struct HeightSmoothingObservation
    {
        public readonly bool WindowHasBridge;
        public readonly double BridgeLerp;
        public readonly double Average;
        public readonly int CorrectionIndex;
        public HeightSmoothingObservation(bool windowHasBridge, double bridgeLerp,
            double average, int correctionIndex)
        {
            WindowHasBridge = windowHasBridge;
            BridgeLerp = bridgeLerp;
            Average = average;
            CorrectionIndex = correctionIndex;
        }
    }

    /// <summary>
    /// Se.smoothHeight / smoothCurvesD and B7.smoothHeight, Dev L02159–L02179,
    /// L02096–L02105, L02133–L02144; Settings L47279–L47288.
    /// Delayed height writes and D writes are called after each eligible coarse
    /// extension, not as a final pass over the whole road.
    /// </summary>
    public static class MidlineSmoothing
    {
        public static HeightSmoothingObservation SmoothHeightAndBank(
            ref CoarseNode windowHead, CoarseNode coarseHead, int window,
            ref double smoothMidlineHeadHeight)
        {
            if (windowHead == null || coarseHead == null) throw new ArgumentNullException();
            if (window < 2) throw new ArgumentOutOfRangeException(nameof(window));
            int half = window / 2;
            CoarseNode node = windowHead, correction = null;
            double average = 0, bridgeLerp = 1;
            bool windowHasBridge = false;
            for (int i = 0; i < window; i++)
            {
                if (node == null || node.Next == null)
                    throw new InvalidOperationException("Smoothing window needs one successor beyond its last sample.");
                if (node.Bridge)
                {
                    windowHasBridge = true;
                    bridgeLerp = Math.Min(bridgeLerp, Math.Abs(i - half) / (double)half);
                }
                average += node.Position.Y;
                node = node.Next; // Source advances before choosing the correction node.
                if (i <= half) correction = node;
            }
            average /= window;
            correction.HeightCorrection = correction.Position.Y - average;
            if (windowHead.Index <= coarseHead.Index + half)
            {
                if (windowHead.Index == coarseHead.Index)
                    smoothMidlineHeadHeight = correction.HeightCorrection;
                windowHead.HeightCorrection = smoothMidlineHeadHeight;
            }
            var oldHead = windowHead;
            oldHead.Position = oldHead.Position.WithY(oldHead.Position.Y - oldHead.HeightCorrection);
            windowHead = oldHead.Next;

            // B7 computes bank on the node just processed by the base method.
            if (oldHead.Previous != null && !oldHead.Previous.Bridge &&
                !oldHead.Bridge && !oldHead.Next.Bridge)
            {
                double delta = oldHead.Next.Heading - oldHead.Previous.Heading;
                if (delta > Math.PI) delta -= Math.PI * 2;
                else if (delta < -Math.PI) delta += Math.PI * 2;
                oldHead.Bank = delta * 0.8;
                oldHead.Bank = oldHead.Bank * 0.5 +
                    (oldHead.Previous.HasBank ? oldHead.Previous.Bank : double.NaN) * 0.5;
            }
            else oldHead.Bank = 0;
            oldHead.HasBank = true;
            return new HeightSmoothingObservation(windowHasBridge, bridgeLerp,
                average, correction.Index);
        }

        public static void SmoothCurves(CoarseNode windowHead, double strength)
        {
            if (strength == 0) return;
            if (windowHead == null) throw new ArgumentNullException(nameof(windowHead));
            if (windowHead.Previous == null) return;
            var previous = windowHead.Previous;
            var next = windowHead.Next ??
                throw new InvalidOperationException("D smoothing requires the next coarse node.");
            double midpointX = (previous.Position.X + next.Position.X) / 2;
            double midpointZ = (previous.Position.Z + next.Position.Z) / 2;
            double deltaAverage = (previous.HeadingDelta + windowHead.HeadingDelta + next.HeadingDelta) / 3;
            double factor = 1 - Math.Max(0.25, Math.Min(1, Math.Abs(deltaAverage) / 0.4));
            factor *= strength;
            var position = windowHead.Position;
            windowHead.Position = new Double3(
                position.X * (1 - factor) + midpointX * factor,
                position.Y,
                position.Z * (1 - factor) + midpointZ * factor);
            windowHead.Heading = MidlineTrig.Atan2(windowHead.Position.Z - previous.Position.Z,
                windowHead.Position.X - previous.Position.X);
            double headingDelta = previous.Heading - windowHead.Heading;
            if (headingDelta > Math.PI) headingDelta -= Math.PI * 2;
            else if (headingDelta < -Math.PI) headingDelta += Math.PI * 2;
            windowHead.HeadingDelta = headingDelta;
        }

        // The rollback branch restores only height, exactly as Dev L01769–L01783.
        // The caller first truncates tail and then invokes this when its condition holds.
        public static void RestoreHeightAfterRollback(ref CoarseNode windowHead,
            CoarseNode tail, int window)
        {
            if (windowHead == null || tail == null) throw new ArgumentNullException();
            if (windowHead.Index <= tail.Index - window) return;
            int start;
            if (windowHead.Index <= tail.Index)
                start = Math.Max(0, tail.Index - windowHead.Index);
            else
            {
                windowHead = tail;
                windowHead.Position = windowHead.Position.WithY(
                    windowHead.Position.Y + windowHead.HeightCorrection);
                start = 0;
            }
            for (int i = start; i <= window; i++)
            {
                if (windowHead.Previous == null) continue;
                windowHead = windowHead.Previous;
                windowHead.Position = windowHead.Position.WithY(
                    windowHead.Position.Y + windowHead.HeightCorrection);
            }
        }
    }
}
