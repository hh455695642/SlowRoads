using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SlowRoads.Environment
{
    // One explicit host-owned background per camera; no road, observer or clock dependencies.
    public sealed class EnvironmentCameraState : IDisposable
    {
        static readonly Dictionary<Camera, EnvironmentCameraState> Active = new();
        public Camera Camera { get; }
        public EnvironmentFrameSettings Frame { get; private set; }
        public EnvironmentSkyResources Sky { get; private set; }
        public EnvironmentCloudResources Clouds { get; private set; }
        public EnvironmentSnowResources Snow { get; private set; }
        public Action PrepareFrame { get; set; }
        public Func<EnvironmentFrameSettings,EnvironmentFrameSettings> FilterFrame {get;set;}
        public int Revision { get; private set; }
        public bool HasVolumeSettings => context.HasSettings;
        readonly EnvironmentCameraContext context=new();
        static readonly Unity.Profiling.ProfilerMarker ResolveMarker=new("Environment.Volume.Resolve");
        string lastError;
        public EnvironmentFrameInput Input;
        bool disposed;
        public EnvironmentCameraState(Camera camera)
        {
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            if (Active.ContainsKey(camera)) throw new InvalidOperationException("Camera already has a background owner");
            Camera = camera; Active.Add(camera, this);
        }
        public static bool TryGet(Camera camera, out EnvironmentCameraState state) => Active.TryGetValue(camera, out state);
        public void Apply(in EnvironmentFrameSettings frame)
        {
            if (disposed) throw new ObjectDisposedException(nameof(EnvironmentCameraState));
            if (!Frame.SameParameters(frame)) Revision++;
            Frame = frame; if (frame.CloudSegments > 0) Clouds?.Configure(frame.CloudSegments);
            if(frame.SnowCount==0)Snow?.Prepare(0);
        }
        public void PrepareFromVolumes(VolumeStack stack)
        {
            using var scope=ResolveMarker.Auto();
            PrepareFrame?.Invoke();
            var input=new EnvironmentFrameInput(Camera.transform.position.y,Input.CloudTime,Input.SnowTime,Input.OriginX,Input.OriginZ);
            if(context.TryUpdate(stack,input,out var error)){lastError=null;ApplyResolved(context.Settings);}
            else {if(context.HasSettings)ApplyResolved(context.Settings);var message=error.ToString();if(lastError!=message){Debug.LogWarning("Environment rejected parameter: "+message,Camera);lastError=message;}}
        }
        void ApplyResolved(in EnvironmentFrameSettings frame)=>Apply(FilterFrame!=null?FilterFrame(frame):frame);
        public void SetSkyMaterial(Material material)
        {
            if (disposed) throw new ObjectDisposedException(nameof(EnvironmentCameraState));
            if (Sky?.Material == material) return;
            Sky?.Dispose(); Sky = material != null ? new EnvironmentSkyResources(material) : null;
        }
        public void SetCloudMaterial(Material material)
        {
            if (disposed) throw new ObjectDisposedException(nameof(EnvironmentCameraState));
            if (Clouds?.Material == material) return;
            Clouds?.Dispose(); Clouds = material != null ? new EnvironmentCloudResources(material) : null;
            if (Frame.CloudSegments > 0) Clouds?.Configure(Frame.CloudSegments);
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            Sky?.Dispose(); Clouds?.Dispose(); Snow?.Dispose();Sky = null; Clouds = null; Snow=null;PrepareFrame=null;FilterFrame=null; Active.Remove(Camera);
        }
        public void SetSnowResources(Material material,ComputeShader compute)
        {
            if(disposed)throw new ObjectDisposedException(nameof(EnvironmentCameraState));
            if(Snow?.Material==material&&Snow?.Compute==compute)return;
            Snow?.Dispose();Snow=material!=null?new EnvironmentSnowResources(material,compute):null;
        }
    }
}
