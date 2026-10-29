using SlowRoads.Generation.Midline;
using SlowRoads.Scene.Road;
using UnityEngine;
namespace SlowRoads.Debugging
{
    public sealed class StreamingRoadRouteSource : IRouteSource
    {
        readonly RoadStreamingPresentation road;
        public StreamingRoadRouteSource(RoadStreamingPresentation road){this.road=road;}
        public int Generation=>road.Core?.Midline.State.Generation??-1;
        public MidlineDistanceResult TrySample(double distance,out Vector3 position,out Vector3 forward)=>road.TrySample(distance,out position,out forward);
    }
}
