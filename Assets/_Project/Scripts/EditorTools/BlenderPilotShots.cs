using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ThreeKingdoms.EditorTools
{
    // Side-by-side shots for the Blender pilot (user 2026-10-26): the town built with the quad buildings, then with the
    // Blender models, each photographed from the same cameras. The scene is left built with the models.
    //   Unity.exe -batchmode -projectPath . -executeMethod ThreeKingdoms.EditorTools.BlenderPilotShots.CaptureFromCommandLine
    //   [-shotDir <folder>]   default D:\BuildArtifacts\Codex\hd2d-jrpg\builds\blender-pilot-shots
    public static class BlenderPilotShots
    {
        // name, look-at point, yaw, pitch, distance, FOV — the first of each pair is the district's own game camera.
        // The house stands just inside the south wall: as in the game (SeeThroughOccluders), the wall in front is cut away.
        static readonly (string name, Vector3 target, float yaw, float pitch, float distance, float fov, string hide)[] Shots =
        {
            ("house_district", new Vector3(-36f, 1.5f, -27.5f), -8f, 30f, 28f, 20f, "South wall W"),
            ("house_close", new Vector3(-36f, 2.2f, -27.5f), -28f, 24f, 18f, 24f, "South wall W"),
            ("gate_outside", new Vector3(0f, 5f, -37f), 0f, 15f, 30f, 24f, null),
            ("gate_close", new Vector3(0f, 5f, -37f), 32f, 18f, 26f, 24f, null),
        };

        public static void CaptureFromCommandLine()
        {
            try { Capture(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        [MenuItem("JRPG/Blender pilot: compare shots")]
        public static void Capture()
        {
            string dir = Arg("-shotDir") ?? @"D:\BuildArtifacts\Codex\hd2d-jrpg\builds\blender-pilot-shots";
            Directory.CreateDirectory(dir);
            foreach (bool blender in new[] { false, true })
            {
                BlenderModels.Enabled = blender; ZhuojunModels.Enabled = blender;
                // The full rebuild (town + story wiring), so the scene left behind is the playable one.
                StoryFlowBuilder.RebuildTown();
                // Comparison shots are about the buildings: no tilt-shift blur toward an absent player (switched back on
                // afterwards, the profile is a shared asset).
                var blur = Object.FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsSortMode.None)
                    .SelectMany(v => v.sharedProfile ? v.sharedProfile.components : new System.Collections.Generic.List<UnityEngine.Rendering.VolumeComponent>())
                    .Where(c => c.GetType().Name == "DepthOfField" && c.active).ToList();
                foreach (var c in blur) c.active = false;
                foreach (string n in new[] { "Plaque", "DoorLeaf_L", "Steps", "plaque_south_gate" })
                    foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                        if (t.name == n && t.GetComponent<Renderer>()) Debug.Log($"BLENDER_PILOT_POS {n} {t.GetComponent<Renderer>().bounds.center}");
                foreach (var s in Shots)
                {
                    var hidden = s.hide == null ? new Renderer[0] : GameObject.Find(s.hide).GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
                    foreach (var r in hidden) r.enabled = false;
                    Shoot(Path.Combine(dir, $"{s.name}_{(blender ? "blender" : "code")}.png"), s.target, s.yaw, s.pitch, s.distance, s.fov);
                    foreach (var r in hidden) r.enabled = true;
                }
                foreach (var c in blur) c.active = true;
            }
            Debug.Log("BLENDER_PILOT_SHOTS " + dir);
        }

        static void Shoot(string file, Vector3 target, float yaw, float pitch, float distance, float fov)
        {
            var rot = Quaternion.Euler(pitch, yaw, 0f);
            var go = new GameObject("Shot camera");
            var cam = go.AddComponent<Camera>();
            var main = Camera.main;
            if (main) { cam.CopyFrom(main); }
            go.transform.SetPositionAndRotation(target - rot * Vector3.forward * distance, rot);
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.5f; cam.farClipPlane = 400f;
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

        static string Arg(string name)
        {
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
