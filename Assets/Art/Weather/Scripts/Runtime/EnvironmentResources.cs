using UnityEngine;

namespace SlowRoads.Environment
{
    [CreateAssetMenu(menuName = "Rendering/Environment Resources")]
    public sealed class EnvironmentResources : ScriptableObject
    {
        public Material skyMaterial, cloudMaterial, snowMaterial;
        public ComputeShader snowCulling;
    }
}
