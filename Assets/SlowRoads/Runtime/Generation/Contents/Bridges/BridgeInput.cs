using System;
using SlowRoads.Generation.Midline;

namespace SlowRoads.Generation.Contents.Bridges
{
    public readonly struct BridgePoint
    {
        public readonly int Index;public readonly Double3 Position,Normal;public readonly bool Bridge;
        public BridgePoint(int index,Double3 position,Double3 normal,bool bridge){Index=index;Position=position;Normal=normal;Bridge=bridge;}
    }
    /// <summary>Frozen P2 bridge interval, including one non-bridge sentinel after the tail.
    /// No placement decisions or mutable producer references are retained.</summary>
    public sealed class BridgeInput
    {
        readonly BridgePoint[] points;
        public readonly int Generation;public readonly long RoadRevision;
        public int Count=>points.Length-1;public BridgePoint Head=>points[0];public BridgePoint Tail=>points[^2];
        public BridgePoint this[int i]=>points[i];
        public BridgeInput(BridgePoint[] interval,int generation,long revision)
        {
            if(interval==null||interval.Length<3||interval.Length>4096)throw new ArgumentOutOfRangeException(nameof(interval));
            for(int i=0;i<interval.Length;i++)
            {
                var p=interval[i];if(!double.IsFinite(p.Position.X+p.Position.Y+p.Position.Z+p.Normal.X+p.Normal.Z)||p.Bridge!=(i<interval.Length-1)||
                    i>0&&p.Index!=interval[i-1].Index+1)throw new ArgumentException("Incomplete or discontinuous original bridge interval");
            }
            points=(BridgePoint[])interval.Clone();Generation=generation;RoadRevision=revision;
        }
    }
}
