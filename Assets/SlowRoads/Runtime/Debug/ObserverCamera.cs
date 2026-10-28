using UnityEngine;

namespace SlowRoads.Debugging
{
    public sealed class ObserverCamera : MonoBehaviour
    {
        Transform target;
        HillsDebugSettings settings;
        public void Initialize(Transform observer, HillsDebugSettings config)
        {
            target = observer;
            settings = config;
            Follow(1f);
        }
        void LateUpdate()
        {
            if (settings != null && settings.cameraFollowEnabled)
                Follow(1f - Mathf.Exp(-Mathf.Max(0f, settings.cameraDamping) * Time.deltaTime));
        }
        void Follow(float blend)
        {
            if (target == null) return;
            var forward = Vector3.ProjectOnPlane(target.forward, Vector3.up).normalized;
            var position = target.position - forward * Mathf.Max(0f, settings.cameraDistance)
                + Vector3.up * Mathf.Max(0.1f, settings.cameraHeight);
            transform.position = Vector3.Lerp(transform.position, position, blend);
            var look = target.position + target.forward * Mathf.Max(0f, settings.lookAhead) - transform.position;
            if (look.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look), blend);
        }
    }
}
