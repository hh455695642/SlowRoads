using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace SlowRoads.Environment
{
    [Serializable, VolumeComponentMenu("Environment/Clouds")]
    public sealed class EnvironmentCloudVolume : VolumeComponent
    {
        public BoolParameter enabled = new(false);
        public ColorParameter highlight = new(Color.white, true, false, true);
        public ColorParameter lowlight = new(Color.gray, true, false, true);
        public FloatParameter shelf0 = new(0), shelf1 = new(0), scale0 = new(1), scale1 = new(1);
        public NoInterpIntParameter mode = new(0);
        public FloatParameter altitude = new(600), worldScale = new(7000), animationSpeed = new(2);
        // Deliberately independent from interpolated worldScale; a scale blend never rebuilds a mesh.
        public NoInterpIntParameter segments = new(20);
    }
}
