using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace SlowRoads.Environment
{
    public readonly struct EnvironmentValidationError
    {
        public readonly string Component, Parameter;
        internal EnvironmentValidationError(string component, string parameter) { Component = component; Parameter = parameter; }
        public override string ToString() => Component == null ? "" : Component + "." + Parameter;
    }

    public static class EnvironmentFrameResolver
    {
        static bool Finite(double v) => double.IsFinite(v) && Math.Abs(v) <= float.MaxValue;
        static bool Finite(Color v) => Finite(v.r) && Finite(v.g) && Finite(v.b) && Finite(v.a);
        static bool Fail(string c, string p, out EnvironmentValidationError error) { error = new(c, p); return false; }
        static bool Atmosphere(EnvironmentAtmosphereVolume v, string name, double cameraHeight, out EnvironmentAtmosphereSettings result, out EnvironmentValidationError error)
        {
            result = default; error = default;
            if (v == null) return Fail(name, "missingComponent", out error);
            if (!Finite(v.colourA.value)) return Fail(name, "colourA", out error);
            if (!Finite(v.colourB.value)) return Fail(name, "colourB", out error);
            if (!Finite(v.colourC.value)) return Fail(name, "colourC", out error);
            double near = v.near.value, far = v.far.value, height = v.hazeHeight.value;
            if (!Finite(near)) return Fail(name, "near", out error);
            if (!Finite(far) || far <= 0 || far <= near || (float)far <= (float)near) return Fail(name, "far", out error);
            if (!Finite(height)) return Fail(name, "hazeHeight", out error);
            if (!Finite(v.hazeIntensity.value)) return Fail(name, "hazeIntensity", out error);
            if ((uint)v.mode.value > 1) return Fail(name, "mode", out error);
            double density = v.densityFromFar.value ? Math.Sqrt(5 / (far * far)) : v.density.value;
            if (!Finite(density) || density < 0) return Fail(name, "density", out error);
            if (v.cameraHeightCorrection.value && height > 0 && cameraHeight < height + 10)
            { double n = Math.Clamp(1 - (cameraHeight - 10) / height, 0, 1); height *= 1 + 2 * n * n; }
            if (!Finite(height)) return Fail(name, "hazeHeight", out error);
            result = new(v.enabled.value, v.colourA.value, v.colourB.value, v.colourC.value,
                new((float)near, (float)far, (float)density, (float)v.mode.value), new((float)height, (float)v.hazeIntensity.value, 0, 0));
            return true;
        }

        // Reads an already evaluated per-camera stack. Does not mutate VolumeManager or any clock.
        public static bool TryResolve(VolumeStack stack, in EnvironmentFrameInput input, out EnvironmentFrameSettings result, out EnvironmentValidationError error)
        {
            result = default; error = default;
            if (stack == null) return Fail("Stack", "missing", out error);
            return TryResolve(stack.GetComponent<EnvironmentSkyVolume>(), stack.GetComponent<EnvironmentFogVolume>(), stack.GetComponent<EnvironmentCloudVolume>(), stack.GetComponent<EnvironmentSnowVolume>(), input, out result, out error);
        }
        public static bool TryResolve(EnvironmentSkyVolume sky,EnvironmentFogVolume fog,EnvironmentCloudVolume cloud,EnvironmentSnowVolume snow,in EnvironmentFrameInput input,out EnvironmentFrameSettings result,out EnvironmentValidationError error)
        {
            result=default;error=default;
            if (!Finite(input.CameraHeight)) return Fail("Input", "cameraHeight", out error);
            if (!double.IsFinite(input.CloudTime) || input.CloudTime < 0) return Fail("Input", "cloudTime", out error);
            if (!double.IsFinite(input.SnowTime) || input.SnowTime < 0) return Fail("Input", "snowTime", out error);
            if (!Finite(input.OriginX) || !Finite(input.OriginZ)) return Fail("Input", "origin", out error);
            if (!Atmosphere(sky, "Sky", input.CameraHeight, out var a, out error) || !Atmosphere(fog, "Fog", input.CameraHeight, out var b, out error)) return false;
            if (cloud == null || snow == null) return Fail("Stack", "missingComponent", out error);
            if (!Finite(sky.backgroundDistance.value) || sky.backgroundDistance.value <= 0) return Fail("Sky", "backgroundDistance", out error);
            if (!Finite(cloud.highlight.value) || !Finite(cloud.lowlight.value)) return Fail("Cloud", "colour", out error);
            if (!Finite(cloud.shelf0.value) || !Finite(cloud.shelf1.value)) return Fail("Cloud", "shelf", out error);
            if (!Finite(cloud.scale0.value) || !Finite(cloud.scale1.value) || cloud.scale0.value == 0 || cloud.scale1.value == 0) return Fail("Cloud", "scale", out error);
            if (!Finite(cloud.altitude.value)) return Fail("Cloud", "altitude", out error);
            if (!Finite(cloud.worldScale.value) || cloud.worldScale.value <= 0) return Fail("Cloud", "worldScale", out error);
            if (!Finite(cloud.animationSpeed.value) || cloud.animationSpeed.value < 0) return Fail("Cloud", "animationSpeed", out error);
            if ((uint)cloud.mode.value > 5) return Fail("Cloud", "mode", out error);
            // Original maximum is floor(22000/400)=55. Bounded extension to 128; no unbounded meshes.
            if (cloud.segments.value < 1 || cloud.segments.value > 128) return Fail("Cloud", "segments", out error);
            if ((uint)snow.quality.value > 2) return Fail("Snow", "quality", out error);
            if (!Finite(snow.colour.value)) return Fail("Snow", "colour", out error);
            if (!Finite(snow.speed.value) || snow.speed.value < 0) return Fail("Snow", "speed", out error);
            result = new(a, b, sky, cloud, snow, input); return true;
        }
    }

    // Owned by exactly one camera. Invalid input keeps its last valid snapshot; no global camera state.
    public sealed class EnvironmentCameraContext
    {
        public int Revision { get; private set; }
        public bool HasSettings { get; private set; }
        public EnvironmentFrameSettings Settings { get; private set; }
        public bool TryUpdate(VolumeStack stack, in EnvironmentFrameInput input, out EnvironmentValidationError error)
        {
            if (!EnvironmentFrameResolver.TryResolve(stack, input, out var next, out error)) return false;
            if (!HasSettings || !Settings.SameParameters(next)) Revision++;
            Settings = next; HasSettings = true; return true;
        }
    }
}
