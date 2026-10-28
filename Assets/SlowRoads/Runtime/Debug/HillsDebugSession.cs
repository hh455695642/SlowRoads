using UnityEngine;

namespace SlowRoads.Debugging
{
    public sealed class HillsDebugSession : MonoBehaviour
    {
        public HillsDebugSettings settings;
        public RouteObserver Observer { get; private set; }
        public SlowRoadsRoad Road { get; private set; }
        GameObject runtimeRoot;
        readonly System.Collections.Generic.List<MeshRenderer> finiteRenderers=new();
        Material lastFiniteMaterial;
        public bool IsFinitePreview=>settings!=null&&settings.useFiniteRoadPreview;
        void Update()
        {
            if(!IsFinitePreview||settings.finiteRoadPreview==null||finiteRenderers.Count==0)return;
            var material=settings.finiteRoadPreview.materials.Resolve();
            if(material==lastFiniteMaterial)return;
            foreach(var renderer in finiteRenderers)if(renderer!=null)renderer.sharedMaterial=material;
            lastFiniteMaterial=material;
        }
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

            finiteRenderers.Clear();lastFiniteMaterial=null;Road=null;
            IRouteSource route;
            if(IsFinitePreview)
            {
                var preview=settings.finiteRoadPreview;
                if(preview==null)throw new System.InvalidOperationException("Bake the finite P4 preview first");
                var material=preview.materials.Resolve();
                for(int i=0;i<preview.meshes.Length;i++)
                {
                    var segment=new GameObject("P4 finite road "+i,typeof(MeshFilter),typeof(MeshRenderer));
                    segment.transform.SetParent(runtimeRoot.transform,false);segment.transform.localPosition=preview.origins[i];
                    segment.GetComponent<MeshFilter>().sharedMesh=preview.meshes[i];
                    var renderer=segment.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.enabled=settings.roadVisible;
                    renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;finiteRenderers.Add(renderer);
                }
                lastFiniteMaterial=material;route=new FiniteRoadRouteSource(preview);
                var sunObject=new GameObject("P4 preview sun",typeof(Light));sunObject.transform.SetParent(runtimeRoot.transform,false);
                sunObject.transform.rotation=Quaternion.Euler(50,-30,0);var sun=sunObject.GetComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.2f;sun.shadows=LightShadows.None;
            }
            else
            {

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
            route=new LegacyRoadRouteSource(Road);
            }

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Route Observer Cube";
            cube.transform.SetParent(runtimeRoot.transform, false);
            cube.transform.localScale = Vector3.one;
            cube.GetComponent<Collider>().enabled = false;
            Destroy(cube.GetComponent<Collider>());
            if (settings.cubeMaterial != null) cube.GetComponent<Renderer>().sharedMaterial = settings.cubeMaterial;
            Observer = cube.AddComponent<RouteObserver>();
            Observer.Initialize(route, settings);

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
