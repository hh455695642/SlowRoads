using System.Collections.Generic;
using SlowRoads.Generation.Midline;
using UnityEngine;

namespace SlowRoads.Rendering.Trees
{
    /// <summary>Shared submission boundary for the existing C1 compute/depth/forward passes.
    /// Each producer still owns its candidate layout, grouping, configuration and buffers.</summary>
    public abstract class HillsGpuSource
    {
        public static readonly HashSet<HillsGpuSource> Active=new();
        public bool Visible=true,CastShadows;public Double3 RenderOrigin;
        public float NearDistance=190,FarDistance=840;
        public virtual bool GroundCover=>false;
        public abstract bool UseCompute {get;}
        public abstract int BatchCount {get;protected set;}
        public abstract IEnumerable<List<TreeGpuBatch>> Clusters {get;}
        public abstract Vector3 Origin(TreeGpuBatch batch);
        public abstract void Bind(TreeGpuBatch batch);
    }
}
