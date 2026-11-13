using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace SlowRoads.Environment
{
    // Preserve source double arithmetic until the final GPU conversion. No keyword variants.
    [Serializable]
    public sealed class EnvironmentDoubleParameter : VolumeParameter<double>
    {
        public EnvironmentDoubleParameter(double value, bool overrideState = false) : base(value, overrideState) { }
        public override void Interp(double from, double to, float t) => m_Value = from + (to - from) * t;
    }

    public enum EnvironmentFogMode { Linear, ExponentialSquared }
    public enum EnvironmentSnowQuality { Off, Half, Full }
    [Serializable] public sealed class EnvironmentFogModeParameter : VolumeParameter<EnvironmentFogMode>
    { public EnvironmentFogModeParameter(EnvironmentFogMode value) : base(value) { } }
    [Serializable] public sealed class EnvironmentSnowQualityParameter : VolumeParameter<EnvironmentSnowQuality>
    { public EnvironmentSnowQualityParameter(EnvironmentSnowQuality value) : base(value) { } }

    [Serializable]
    public abstract class EnvironmentAtmosphereVolume : VolumeComponent
    {
        public BoolParameter enabled = new(false);
        // These are linear radiometric colours. Adapters convert source sRGB exactly once.
        public ColorParameter colourA = new(Color.black, true, false, true);
        public ColorParameter colourB = new(Color.black, true, false, true);
        public ColorParameter colourC = new(Color.black, true, false, true);
        public EnvironmentDoubleParameter near = new(0);
        public EnvironmentDoubleParameter far = new(1000);
        public EnvironmentFogModeParameter mode = new(EnvironmentFogMode.Linear);
        public BoolParameter densityFromFar = new(true);
        public EnvironmentDoubleParameter density = new(0);
        public EnvironmentDoubleParameter hazeHeight = new(0);
        public EnvironmentDoubleParameter hazeIntensity = new(0);
        public BoolParameter cameraHeightCorrection = new(true);
    }
}
