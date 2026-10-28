using UnityEngine;

namespace SlowRoads.Debugging
{
    [CreateAssetMenu(menuName = "Slow Roads/Hills Debug Settings")]
    public sealed class HillsDebugSettings : ScriptableObject
    {
        public int seed = 1337;
        public RoadGenConfig road = RoadGenConfig.Default;
        [Range(8, 256)] public int aheadNodes = 120;
        [Range(2, 64)] public int behindNodes = 30;
        public bool movementEnabled = true;
        public bool cameraFollowEnabled = true;
        public bool roadVisible = true;
        public bool debugGizmos = true;
        [Min(0)] public float speed = 8f;
        [Min(0.01f)] public float stepMetres = 1f;
        [Min(0)] public float cameraDistance = 14f;
        [Min(0.1f)] public float cameraHeight = 8f;
        [Min(0)] public float lookAhead = 8f;
        [Min(0)] public float cameraDamping = 6f;
        public Material roadMaterial;
        public Material cubeMaterial;
        [Header("P4 finite material preview (no streaming)")]
        public bool useFiniteRoadPreview;
        public SlowRoads.Scene.Road.HillsRoadFinitePreview finiteRoadPreview;
    }
}
