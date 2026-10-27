using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ThreeKingdoms.EditorTools
{
    // Bohai Codex + Blender rebuild (user 2026-10-27): builds the plain with the quad-built pieces, photographs it, then
    // builds it again with the delivered Blender sets and photographs the same cameras. The scene left on disk is the
    // Blender one. production/levels/bohai-plain/blender-rebuild.md.
    //   Unity.exe -batchmode -projectPath . -executeMethod ThreeKingdoms.EditorTools.BohaiBlenderShots.CaptureFromCommandLine
    //   [-blenderFrom <staging folder>] [-shotSet pingyuan]
    public static class BohaiBlenderShots
    {
        // name, look-at point, yaw, pitch, distance, FOV.
        static readonly (string name, Vector3 target, float yaw, float pitch, float distance, float fov)[] Pingyuan =
        {
            ("pingyuan_square", new Vector3(-60f, 1f, -150f), 0f, 25f, 34f, 20f),
            ("pingyuan_hall", new Vector3(-60f, 3f, -137f), 25f, 20f, 30f, 22f),
            ("pingyuan_street", new Vector3(-60f, 2f, -180f), -20f, 28f, 40f, 22f),
            ("pingyuan_south_gate", new Vector3(-60f, 3f, -196f), 0f, 18f, 34f, 22f),
            ("pingyuan_corner", new Vector3(-110f, 2f, -195f), 35f, 22f, 32f, 22f),
            ("pingyuan_overview", new Vector3(-60f, 0f, -155f), 0f, 45f, 150f, 30f),
        };

        public static void CaptureFromCommandLine()
        {
            try { Capture(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        [MenuItem("JRPG/Blender: Bohai comparison shots")]
        public static void Capture()
        {
            string dir = @"D:\BuildArtifacts\Codex\hd2d-jrpg\builds\bohai-blender-shots";
            Directory.CreateDirectory(dir);
            foreach (bool blender in new[] { false, true })
            {
                BlenderModels.Enabled = blender;
                BohaiPlainBuilder.Build();
                var blur = Object.FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsSortMode.None)
                    .SelectMany(v => v.sharedProfile ? v.sharedProfile.components : new System.Collections.Generic.List<UnityEngine.Rendering.VolumeComponent>())
                    .Where(c => c.GetType().Name == "DepthOfField" && c.active).ToList();
                foreach (var c in blur) c.active = false;
                foreach (string n in new[] { "People", "Whitebox labels", "Labels" })
                {
                    var t = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).FirstOrDefault(x => x.name == n && !x.parent);
                    if (t) t.gameObject.SetActive(false);
                }
                foreach (var s in Pingyuan) Shoot(Path.Combine(dir, $"{s.name}_{(blender ? "blender" : "code")}.png"), s);
                foreach (var c in blur) c.active = true;
            }
            AssetDatabase.SaveAssets();
            Debug.Log("BOHAI_BLENDER_SHOTS " + dir);
        }

        static void Shoot(string file, (string name, Vector3 target, float yaw, float pitch, float distance, float fov) s)
        {
            var rot = Quaternion.Euler(s.pitch, s.yaw, 0f);
            var go = new GameObject("Shot camera");
            var cam = go.AddComponent<Camera>();
            if (Camera.main) cam.CopyFrom(Camera.main);
            go.transform.SetPositionAndRotation(s.target - rot * Vector3.forward * s.distance, rot);
            cam.fieldOfView = s.fov;
            cam.nearClipPlane = 0.5f; cam.farClipPlane = 900f;
            var data = go.GetComponent<UniversalAdditionalCameraData>() ?? go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            tex.Apply();
            File.WriteAllBytes(file, tex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = null;
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(go);
        }
    }
}
