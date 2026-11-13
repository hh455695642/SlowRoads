using UnityEngine;

namespace SlowRoads.Environment
{
    public readonly struct EnvironmentAtmosphereSettings
    {
        public readonly bool Enabled;
        public readonly Vector4 A, B, C, Range, Haze;
        public EnvironmentAtmosphereSettings(bool enabled, Vector4 a, Vector4 b, Vector4 c, Vector4 range, Vector4 haze)
        { Enabled = enabled; A = a; B = b; C = c; Range = range; Haze = haze; }
        public bool Same(in EnvironmentAtmosphereSettings other) => Enabled == other.Enabled && A.Equals(other.A) && B.Equals(other.B) && C.Equals(other.C) && Range.Equals(other.Range) && Haze.Equals(other.Haze);
    }

    public readonly struct EnvironmentFrameInput
    {
        public readonly double CameraHeight, CloudTime, SnowTime, OriginX, OriginZ;
        // The owner advances clocks once. Rendering a second camera never advances them here.
        public EnvironmentFrameInput(double cameraHeight, double cloudTime, double snowTime, double originX = 0, double originZ = 0)
        { CameraHeight = cameraHeight; CloudTime = cloudTime; SnowTime = snowTime; OriginX = originX; OriginZ = originZ; }
    }

    // Value-only RenderGraph input: no VolumeComponent, mutable Material or static camera references.
    public readonly struct EnvironmentFrameSettings
    {
        public readonly EnvironmentAtmosphereSettings Sky, Fog;
        public readonly bool CloudsEnabled, SnowEnabled;
        public readonly float BackgroundDistance;
        public readonly Vector4 CloudHighlight, CloudLowlight, CloudShelf, CloudGeometry, SnowColour;
        public readonly int CloudMode, CloudSegments, SnowCount;
        public readonly float SnowSpeed;
        public readonly EnvironmentFrameInput Input;
        // Camera visibility masks never mutate a shared VolumeStack or another camera's snapshot.
        public EnvironmentFrameSettings WithVisibility(bool sky,bool fog)=>new(this,sky,fog);
        EnvironmentFrameSettings(in EnvironmentFrameSettings f,bool sky,bool fog)
        {
            Sky=new(f.Sky.Enabled&&sky,f.Sky.A,f.Sky.B,f.Sky.C,f.Sky.Range,f.Sky.Haze);
            Fog=new(f.Fog.Enabled&&fog,f.Fog.A,f.Fog.B,f.Fog.C,f.Fog.Range,f.Fog.Haze);
            CloudsEnabled=f.CloudsEnabled&&sky;SnowEnabled=f.SnowEnabled;
            BackgroundDistance=f.BackgroundDistance;CloudHighlight=f.CloudHighlight;CloudLowlight=f.CloudLowlight;
            CloudShelf=f.CloudShelf;CloudGeometry=f.CloudGeometry;CloudMode=f.CloudMode;CloudSegments=f.CloudSegments;
            SnowColour=f.SnowColour;SnowSpeed=f.SnowSpeed;
            SnowCount=f.SnowCount;Input=f.Input;
        }
        internal EnvironmentFrameSettings(EnvironmentAtmosphereSettings sky, EnvironmentAtmosphereSettings fog, EnvironmentSkyVolume s, EnvironmentCloudVolume c, EnvironmentSnowVolume snow, in EnvironmentFrameInput input)
        {
            Sky = sky; Fog = fog; BackgroundDistance = (float)s.backgroundDistance.value;
            CloudsEnabled = c.enabled.value; SnowEnabled = snow.enabled.value;
            CloudHighlight = c.highlight.value; CloudLowlight = c.lowlight.value;
            CloudShelf = new(c.shelf0.value, c.shelf1.value, c.scale0.value, c.scale1.value);
            CloudGeometry = new(c.altitude.value, c.worldScale.value, c.animationSpeed.value, 0);
            CloudMode = c.mode.value; CloudSegments = c.segments.value;
            SnowColour = snow.colour.value; SnowSpeed = snow.speed.value;
            SnowCount = !SnowEnabled ? 0 : snow.quality.value switch { EnvironmentSnowQuality.Half => 5476, EnvironmentSnowQuality.Full => 48841, _ => 0 };
            Input = input;
        }
        // Time and logical origin are frame input, not parameter revisions.
        public bool SameParameters(in EnvironmentFrameSettings x) => Sky.Same(x.Sky) && Fog.Same(x.Fog) && BackgroundDistance.Equals(x.BackgroundDistance)
            && CloudsEnabled == x.CloudsEnabled && SnowEnabled == x.SnowEnabled
            && CloudHighlight.Equals(x.CloudHighlight) && CloudLowlight.Equals(x.CloudLowlight) && CloudShelf.Equals(x.CloudShelf) && CloudGeometry.Equals(x.CloudGeometry)
            && CloudMode == x.CloudMode && CloudSegments == x.CloudSegments
            && SnowColour.Equals(x.SnowColour) && SnowSpeed.Equals(x.SnowSpeed) && SnowCount == x.SnowCount;
    }
}
