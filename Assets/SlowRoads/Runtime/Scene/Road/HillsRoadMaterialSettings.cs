using System;
using UnityEngine;

namespace SlowRoads.Scene.Road
{
    public enum HillsRoadSeason { Summer, Spring, Autumn, Winter }
    public enum HillsRoadSurface { Standard, Single, SingleWide }
    [Serializable] public sealed class HillsRoadMaterialEntry
    { public HillsRoadSeason season;public HillsRoadSurface surface;public Material material; }

    // Extension point: S2 replaces serialized materials; generation and its random streams never read this SO.
    [CreateAssetMenu(menuName="Slow Roads/Hills Road Material Settings")]
    public sealed class HillsRoadMaterialSettings : ScriptableObject
    {
        public bool surfaceEnabled=true,grayPreview;
        public HillsRoadSeason season;
        public HillsRoadSurface surface;
        public Material grayMaterial;
        public HillsRoadMaterialEntry[] entries=Array.Empty<HillsRoadMaterialEntry>();
        public Material Resolve()
        {
            if(!surfaceEnabled||grayPreview)return grayMaterial;
            foreach(var entry in entries)if(entry.season==season&&entry.surface==surface)
            {if(entry.material==null||entry.material.shader==null||!entry.material.shader.isSupported)throw new InvalidOperationException("Hills road material is unavailable");return entry.material;}
            throw new InvalidOperationException("No Hills road material for the selected surface and season");
        }
    }
}
