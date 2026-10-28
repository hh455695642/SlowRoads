using UnityEngine;

namespace SlowRoads.Debugging
{
    public sealed class HillsDebugSession : MonoBehaviour
    {
        public HillsDebugSettings settings;
        public RouteObserver Observer { get; private set; }
        public SlowRoadsRoad Road { get; private set; }
        GameObject runtimeRoot;
        void Start() => Rebuild();

        [ContextMenu("Reset Fixed Seed")]
        public void Rebuild()
        {
            if (!Application.isPlaying || settings == null) return;
            if (runtimeRoot != null)
            {
                runtimeRoot.SetActive(false);
                Destroy(runtimeRoot);
            }
            runtimeRoot = new GameObject("Hills Debug Runtime");
            runtimeRoot.transform.SetParent(transform, false);

            var roadObject = new GameObject("Legacy Route Preview (not parity)");
            // 配置在 Awake 之前注入，避免 AddComponent 提前使用默认 seed。
            roadObject.SetActive(false);
            roadObject.transform.SetParent(runtimeRoot.transform, false);
            Road = roadObject.AddComponent<SlowRoadsRoad>();
            Road.seed = settings.seed;
            Road.gen = settings.road;
            Road.gen.nodeSpacing = Mathf.Max(0.1f, Road.gen.nodeSpacing);
            Road.gen.feelDist = Mathf.Max(0.1f, Road.gen.feelDist);
            Road.gen.maxGrad = Mathf.Max(0.001f, Road.gen.maxGrad);
            Road.horizonAhead = Mathf.Clamp(settings.aheadNodes, 8, 256);
            Road.keepBehind = Mathf.Clamp(settings.behindNodes, 2, 64);
            Road.showGizmos = settings.debugGizmos;
            roadObject.SetActive(true);
            if (settings.roadMaterial != null)
                Road.GetComponent<MeshRenderer>().sharedMaterial = settings.roadMaterial;
            Road.GetComponent<MeshRenderer>().enabled = settings.roadVisible;

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Route Observer Cube";
            cube.transform.SetParent(runtimeRoot.transform, false);
            cube.transform.localScale = Vector3.one;
            cube.GetComponent<Collider>().enabled = false;
            Destroy(cube.GetComponent<Collider>());
            if (settings.cubeMaterial != null) cube.GetComponent<Renderer>().sharedMaterial = settings.cubeMaterial;
            Observer = cube.AddComponent<RouteObserver>();
            Observer.Initialize(new LegacyRoadRouteSource(Road), settings);

            var cameraObject = new GameObject("Debug Camera", typeof(Camera));
            cameraObject.transform.SetParent(runtimeRoot.transform, false);
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.36f, 0.53f, 0.65f);
            camera.farClipPlane = 1800f;
            cameraObject.AddComponent<ObserverCamera>().Initialize(cube.transform, settings);
        }
        [ContextMenu("Toggle Pause")]
        public void TogglePause() { if (Observer != null) Observer.paused = !Observer.paused; }
        [ContextMenu("Step Forward")]
        public void Step()
        {
            if (Observer == null) return;
            Observer.paused = true;
            Observer.Move(Mathf.Max(0.01f, settings.stepMetres));
        }
        void OnDestroy() { if (runtimeRoot != null) Destroy(runtimeRoot); }
    }
}
