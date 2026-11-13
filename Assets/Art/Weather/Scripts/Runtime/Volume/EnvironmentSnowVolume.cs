using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace SlowRoads.Environment
{
    [Serializable, VolumeComponentMenu("Environment/Snow")]
    public sealed class EnvironmentSnowVolume : VolumeComponent
    {
        public BoolParameter enabled = new(false);
        public EnvironmentSnowQualityParameter quality = new(EnvironmentSnowQuality.Off);
        public ColorParameter colour = new(Color.white, true, false, true);
        public FloatParameter speed = new(1);
    }
}
