using UnityEngine;

namespace SlowRoads.Debugging
{
    // 仅沿弧长观察场景：没有 Rigidbody、输入、转向、动力或车辆状态。
    public sealed class RouteObserver : MonoBehaviour
    {
        public bool paused;
        public float Distance { get; private set; }
        IRouteSource route;
        HillsDebugSettings settings;
        public void Initialize(IRouteSource source, HillsDebugSettings config)
        {
            route = source;
            settings = config;
            Distance = 0f;
            Move(0f);
        }
        void Update()
        {
            if (settings != null && settings.movementEnabled && !paused)
                Move(Mathf.Max(0f, settings.speed) * Time.deltaTime);
        }
        public void Move(float metres)
        {
            if (route == null) return;
            Distance += Mathf.Max(0f, metres);
            route.EnsureAhead(Distance + 1f);
            route.Sample(Distance, out var position, out var forward);
            transform.SetPositionAndRotation(position + Vector3.up * 0.65f,
                Quaternion.LookRotation(forward, Vector3.up));
        }
    }
}
