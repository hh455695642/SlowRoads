using UnityEngine;

namespace SlowRoads.Debugging
{
    // 扩展点：精中线移植后替换适配器；观察者不依赖车辆、地形网格或渲染器。
    public interface IRouteSource
    {
        void EnsureAhead(float distance);
        void Sample(float distance, out Vector3 position, out Vector3 forward);
    }

}

