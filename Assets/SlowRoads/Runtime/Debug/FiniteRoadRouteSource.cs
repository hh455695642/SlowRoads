using System;
using SlowRoads.Scene.Road;
using UnityEngine;

namespace SlowRoads.Debugging
{
    public interface IBoundedRouteSource { float MaximumDistance {get;} }
    public sealed class FiniteRoadRouteSource : IRouteSource, IBoundedRouteSource
    {
        readonly Vector3[] points;
        readonly float[] distances;
        public float MaximumDistance=>distances[distances.Length-1];
        public FiniteRoadRouteSource(HillsRoadFinitePreview preview)
        {
            if(preview==null||preview.route==null||preview.route.Length<2)throw new ArgumentException("Finite P4 route is missing");
            points=preview.route;distances=new float[points.Length];
            for(int i=1;i<points.Length;i++)distances[i]=distances[i-1]+Vector3.Distance(points[i-1],points[i]);
        }
        public void EnsureAhead(float distance){} // Entire finite route is baked; observer clamps before Sample.
        public void Sample(float distance,out Vector3 position,out Vector3 forward)
        {
            if(distance<0||distance>MaximumDistance)throw new ArgumentOutOfRangeException(nameof(distance));
            int lo=1,hi=points.Length-1;while(lo<hi){int m=(lo+hi)/2;if(distances[m]<distance)lo=m+1;else hi=m;}
            int end=lo,begin=end-1;float length=distances[end]-distances[begin];
            if(length<=0)throw new InvalidOperationException("Degenerate finite preview route");
            position=Vector3.Lerp(points[begin],points[end],(distance-distances[begin])/length);forward=(points[end]-points[begin])/length;
        }
    }
}
