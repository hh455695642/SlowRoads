using System;
using UnityEngine;

namespace SlowRoads.Rendering.Environment
{
    /// <summary>Original ls/updateHDR values, independent of Volume, camera and material lifetime.</summary>
    public readonly struct HillsThemeLighting
    {
        public readonly bool Enabled;
        public readonly Color Sun, Ambient, Hemisphere, WaterBase, WaterBody, WaterHighlight;
        public readonly float ReferenceSunIntensity;
        public readonly Vector4 Theme;

        HillsThemeLighting(HillsEnvironmentProfile profile, double observerShadow, bool enabled)
        {
            var lights = profile.Lights;
            Enabled = enabled;
            ReferenceSunIntensity = lights.SunIntensity;
            // Performance: value-only evaluation, no SH rebuild, RNG, material allocation or render pass.
            // Settings-full L52495–52506: preserve double order and the absence of a lower clamp.
            float intensity = (float)(lights.AmbientIntensity *
                (1 - .2 * observerShadow * Math.Min(1, (lights.SunIntensity - 3) / 2d)));
            Color ambient = HillsEnvironmentProfile.LinearColour(lights.Ambient);
            if (lights.AmbientShadow != 0)
                ambient = Color.LerpUnclamped(ambient, HillsEnvironmentProfile.LinearColour(5471792), (float)observerShadow);
            Sun = HillsEnvironmentProfile.LinearColour(lights.Sun) * (lights.SunIntensity / Mathf.PI);
            Ambient = ambient * (intensity / Mathf.PI);
            Hemisphere = HillsEnvironmentProfile.LinearColour(lights.Sun) * (intensity / Mathf.PI);
            // Extension point: consumers map these source values to their own material property names.
            Theme = new Vector4(profile.ShadowFactor, lights.Radiance, lights.Fresnel, profile.TreeDiscolouration);
            WaterBase = HillsEnvironmentProfile.LinearColour(profile.WaterBase);
            WaterBody = HillsEnvironmentProfile.LinearColour(profile.WaterBody);
            WaterHighlight = HillsEnvironmentProfile.LinearColour(profile.WaterHighlight);
        }

        public static HillsThemeLighting Evaluate(HillsEnvironmentProfile profile, double observerShadow, bool enabled = true)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (!double.IsFinite(observerShadow)) throw new ArgumentOutOfRangeException(nameof(observerShadow));
            return new HillsThemeLighting(profile, observerShadow, enabled);
        }
    }
}
