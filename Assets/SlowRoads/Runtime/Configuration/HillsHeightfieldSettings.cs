using SlowRoads.Generation.Heightfield;
using UnityEngine;

namespace SlowRoads.Configuration
{
    /// <summary>Inspector data only. The generator snapshots this when it is constructed.</summary>
    [CreateAssetMenu(menuName = "Slow Roads/Hills Heightfield Settings")]
    public sealed class HillsHeightfieldSettings : ScriptableObject
    {
        public string seed = "a";
        public double scale = 60;
        public double offset = 100;
        public int resolution = 5;
        public int upresFactor = 2;
        public int depth = 5;
        public int[] layerResolutions = { 3, 9, 13, 27, 31 };
        public double depthHeightFactor = 1.25;
        [Tooltip("Original Hills road style 3 (L01698-L01709). Snapshot before generation.")]
        public bool squared = true;
        public bool temper = true;
        public double temperBase = 50;
        public double temperBelow = 500;
        public double temperMin = 0.5;
        public bool spiralise = true;
        [Tooltip("Original graphics detail 2 cache threshold; not a strict capacity limit.")]
        public int maxCached = 20;
        public double tileScaleRandom = 0.25;
        public bool repeated;

        // Extension point: the later midline owns layer switching; it receives a stable config snapshot.
        public HillsHeightfieldConfig Snapshot() => new HillsHeightfieldConfig(
            seed, scale, offset, resolution, upresFactor, depth, layerResolutions,
            depthHeightFactor, squared, temper, temperBase, temperBelow, temperMin,
            spiralise, maxCached, tileScaleRandom, repeated);
    }
}
