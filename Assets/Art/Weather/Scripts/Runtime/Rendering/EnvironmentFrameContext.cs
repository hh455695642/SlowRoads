using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace SlowRoads.Environment
{
    // One resolution per camera render graph, even when the surface feature is disabled.
    public sealed class EnvironmentFrameContext : ContextItem
    {
        bool resolved;EnvironmentCameraState state;
        public static EnvironmentCameraState Resolve(ContextContainer frame)
        {
            var data=frame.GetOrCreate<EnvironmentFrameContext>();
            if(!data.resolved){data.resolved=true;EnvironmentCameraState.TryGet(frame.Get<UniversalCameraData>().camera,out data.state);data.state?.PrepareFromVolumes(VolumeManager.instance.stack);}
            return data.state;
        }
        public override void Reset(){resolved=false;state=null;}
    }
}
