using UnityEngine;
using UnityEngine.Rendering;

namespace Jrpg
{
    public sealed class FollowCamera : MonoBehaviour
    {
        public Transform Target;
        public Vector3 Offset = new Vector3(0, 8, -10);

        // Shot rig (towns): when UseRig is on the camera is placed from pitch / distance / yaw / FOV / look offset
        // instead of the fixed Offset, and eases toward the CameraZone the target stands in.
        public bool UseRig;
        public float Pitch = 25f, Distance = 34f, Yaw, Fov = 20f;
        public Vector3 LookOffset = new Vector3(0, 0.8f, 0);
        [Tooltip("Seconds to ease most of the way into a new zone's shot.")] public float BlendSeconds = 1.1f;
        [Tooltip("Global volume whose depth of field keeps focus on the target as the distance changes.")] public Volume FocusVolume;

        float pitch, distance, yaw, fov, sway;
        Vector3 look;
        bool started;

        public CameraZone ActiveZone { get; private set; }
        // A story scene's own shot distance (> 0) over any zone's.
        public float StageDistance { get; set; }
        // A short shake (a blow in a story scene), in seconds left.
        public float ShakeSeconds { get; set; }
        float zoneBlend;

        void LateUpdate()
        {
            if (!Target || Paused) return;
            if (!UseRig) { transform.position = Target.position + Offset; return; }

            ActiveZone = null;
            foreach (var z in CameraZone.All)
                if (z.Contains(Target.position) && (ActiveZone == null || z.Priority > ActiveZone.Priority)) ActiveZone = z;
            float tp = ActiveZone ? ActiveZone.Pitch : Pitch, td = StageDistance > 0f ? StageDistance : ActiveZone ? ActiveZone.Distance : Distance;
            float ty = ActiveZone ? ActiveZone.Yaw : Yaw, tf = ActiveZone ? ActiveZone.Fov : Fov, ts = ActiveZone ? ActiveZone.Sway : 0f;
            Vector3 tl = ActiveZone ? ActiveZone.LookOffset : LookOffset;
            if (!started) { pitch = tp; distance = td; yaw = ty; fov = tf; look = tl; sway = ts; started = true; }
            else
            {
                // Exponential ease: framerate independent, about 95 % of the way after BlendSeconds.
                // A zone may set its own pace (the Zhuojun square lowers slowly, user 2026-10-26); leaving it eases back at the same pace.
                if (ActiveZone) zoneBlend = ActiveZone.BlendSeconds;
                float blend = zoneBlend > 0f ? zoneBlend : BlendSeconds;
                float k = 1f - Mathf.Exp(-3f * Time.deltaTime / Mathf.Max(0.05f, blend));
                pitch = Mathf.Lerp(pitch, tp, k); distance = Mathf.Lerp(distance, td, k); yaw = Mathf.Lerp(yaw, ty, k);
                fov = Mathf.Lerp(fov, tf, k); look = Vector3.Lerp(look, tl, k); sway = Mathf.Lerp(sway, ts, k);
            }
            float swing = sway * Mathf.Sin(Time.time * 0.35f);
            if (AvoidTerrain) LiftOverTerrain(Target.position + look, yaw + swing);
            var rotation = Quaternion.Euler(pitch + lift, yaw + swing, 0f);
            Vector3 shake = Vector3.zero;
            if (ShakeSeconds > 0f) { ShakeSeconds -= Time.deltaTime; shake = Random.insideUnitSphere * 0.12f; }
            transform.SetPositionAndRotation(Target.position + look + rotation * new Vector3(0, 0, -distance) + shake, rotation);
            var cam = GetComponent<Camera>();
            if (cam) cam.fieldOfView = fov;
            if (FocusVolume) Focus(FocusVolume, distance);
        }

        // Open regions (R1 涿蓟, user 2026-10-28): on a steep slope the ground between the party and the camera would fill
        // the shot, so the camera tilts up over the ridge (quickly) and settles back once the view is clear (slowly).
        // Only the terrain counts ("Terrain …" mesh colliders); trees and buildings are left to the see-through occluders.
        public bool AvoidTerrain;
        float lift;

        void LiftOverTerrain(Vector3 focus, float heading)
        {
            float need = 0f;
            for (float extra = 0f; pitch + extra <= 80f; extra += 4f)
            {
                need = extra;
                var back = Quaternion.Euler(pitch + extra, heading, 0f) * Vector3.back;
                bool blocked = false;
                // Cast from the camera's spot toward the party: mesh colliders only stop rays that meet their front face.
                var from = focus + back * distance;
                foreach (var h in Physics.RaycastAll(from, -back, distance - 1.5f, ~0, QueryTriggerInteraction.Ignore))
                    if (h.collider is MeshCollider && h.collider.name.StartsWith("Terrain")) { blocked = true; break; }
                if (!blocked) break;
            }
            float rate = need > lift ? 6f : 1.2f;
            lift = Mathf.Lerp(lift, need, 1f - Mathf.Exp(-rate * Time.deltaTime));
        }

        // Jump straight to the current target's shot (after a teleport or scene load) instead of easing in.
        public void Snap() => started = false;

        // A scripted shot (the first arrival in Zhuojun, user 2026-10-26) moves the camera itself while this is set.
        public bool Paused;
        public void FocusAt(float metres) { if (FocusVolume) Focus(FocusVolume, metres); }

        // Keeps the tilt-shift focus on the target as the distance changes. The URP depth-of-field override lives in
        // another assembly, so it is handled by type name. A small overlay volume carries a copy of the scene profile's
        // depth of field; the scene volume's own profile (swapped by the quality tier) is never modified.
        Volume overlay;
        VolumeComponent overlayDof;

        void Focus(Volume scene, float focus)
        {
            VolumeComponent source = null;
            if (scene.sharedProfile)
                foreach (var c in scene.sharedProfile.components) if (c.GetType().Name == "DepthOfField") source = c;
            if (overlayDof == null)
            {
                if (source == null) return;
                overlay = gameObject.AddComponent<Volume>();
                overlay.isGlobal = true;
                overlay.priority = scene.priority + 1;
                overlay.sharedProfile = ScriptableObject.CreateInstance<VolumeProfile>();
                overlayDof = Instantiate(source);
                overlay.sharedProfile.components.Add(overlayDof);
            }
            overlayDof.active = source != null && source.active;   // the low tier may have no depth of field
            if (overlayDof.GetType().GetField("focusDistance")?.GetValue(overlayDof) is VolumeParameter<float> p) { p.overrideState = true; p.value = focus; }
        }
    }
}
