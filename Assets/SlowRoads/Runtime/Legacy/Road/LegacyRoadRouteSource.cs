using UnityEngine;

namespace SlowRoads.Debugging
{
    public sealed class LegacyRoadRouteSource : IRouteSource
    {
        readonly SlowRoadsRoad road;
        public LegacyRoadRouteSource(SlowRoadsRoad road) { this.road = road; }
        public void EnsureAhead(float distance) => road.Sync(distance);
        public void Sample(float distance, out Vector3 position, out Vector3 forward)
        {
            road.Sample(distance, out position, out _, out _);
            road.Sample(distance + 0.25f, out var next, out var fallback, out _);
            forward = (next - position).sqrMagnitude > 0.000001f ? (next - position).normalized : fallback;
            position += Vector3.up * road.gen.roadHeight;
        }
    }
}

