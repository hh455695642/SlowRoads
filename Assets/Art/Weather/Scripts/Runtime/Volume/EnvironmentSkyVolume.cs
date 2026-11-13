using System;
using UnityEngine.Rendering;

namespace SlowRoads.Environment
{
    [Serializable, VolumeComponentMenu("Environment/Sky")]
    public sealed class EnvironmentSkyVolume : EnvironmentAtmosphereVolume
    {
        public EnvironmentDoubleParameter backgroundDistance = new(1680);
    }
}
