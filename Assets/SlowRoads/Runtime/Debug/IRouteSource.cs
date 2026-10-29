using SlowRoads.Generation.Midline;
using UnityEngine;
namespace SlowRoads.Debugging
{
    public interface IRouteSource
    {
        int Generation {get;}
        MidlineDistanceResult TrySample(double distance,out Vector3 position,out Vector3 forward);
    }
}
