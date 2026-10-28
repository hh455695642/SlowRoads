using UnityEngine;

namespace SlowRoads.Scene.Road
{
    // Baked by the explicit Editor P4 builder, not a streaming scene generator.
    // Meshes remain asset-owned; preview objects never destroy them.
    public sealed class HillsRoadFinitePreview : ScriptableObject
    {
        public Mesh[] meshes;
        public Vector3[] origins;
        public Vector3[] route;
        public HillsRoadMaterialSettings materials;
        public string sourceCase;
    }
}
