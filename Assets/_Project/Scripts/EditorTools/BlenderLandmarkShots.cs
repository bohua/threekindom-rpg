using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ThreeKingdoms.EditorTools
{
    // Zhongshan landmarks in Blender (user 2026-10-26: "试着在大地图上选几个景点做一下比较，比如烽火台，岩洞啥的"): builds
    // R2 as it is, photographs the rock shrine, the old beacon and the Black Mountain stockade, then swaps in the Codex
    // Blender models (art/zhongshan-blender-landmarks) at the same places and photographs them again from the same
    // cameras. The scene on disk is left as ZhongshanBuilder made it; the swap is for the comparison only.
    //   Unity.exe -batchmode -projectPath . -executeMethod ThreeKingdoms.EditorTools.BlenderLandmarkShots.CaptureFromCommandLine
    public static class BlenderLandmarkShots
    {
        const string Set = "zhongshan-blender-landmarks";

        // name, look-at point, yaw, pitch, distance, FOV. The stockade faces east, so its 3/4 shot looks north-west.
        static readonly (string name, Vector3 target, float yaw, float pitch, float distance, float fov)[] Shots =
        {
            ("shrine_game", new Vector3(93f, 2.5f, 176f), 0f, 25f, 34f, 20f),
            ("shrine_close", new Vector3(93f, 3f, 176f), 28f, 20f, 24f, 22f),
            ("beacon_game", new Vector3(151f, 3.5f, 234f), 0f, 25f, 34f, 20f),
            ("beacon_close", new Vector3(151f, 4f, 234f), -30f, 18f, 24f, 22f),
            ("stockade_game", new Vector3(96f, 2.5f, 74f), 0f, 25f, 34f, 20f),
            ("stockade_close", new Vector3(97f, 3f, 73f), -60f, 20f, 28f, 22f),
        };

        public static void CaptureFromCommandLine()
        {
            try { Capture(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        [MenuItem("JRPG/Blender: Zhongshan landmark shots")]
        public static void Capture()
        {
            string dir = @"D:\BuildArtifacts\Codex\hd2d-jrpg\builds\blender-landmarks-shots";
            Directory.CreateDirectory(dir);
            ZhongshanBuilder.Build();
            var blur = Object.FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsSortMode.None)
                .SelectMany(v => v.sharedProfile ? v.sharedProfile.components : new System.Collections.Generic.List<UnityEngine.Rendering.VolumeComponent>())
                .Where(c => c.GetType().Name == "DepthOfField" && c.active).ToList();
            foreach (var c in blur) c.active = false;
            RenderSettings.fog = false;
            HidePeople();
            foreach (var s in Shots) Shoot(Path.Combine(dir, s.name + "_code.png"), s);

            if (!BlenderModels.Import(Set)) throw new System.Exception("Codex delivery art/" + Set + " not found");
            Swap();
            foreach (var s in Shots) Shoot(Path.Combine(dir, s.name + "_blender.png"), s);
            foreach (var c in blur) c.active = true;
            AssetDatabase.SaveAssets();
            // Leave the built scene as it was on disk.
            EditorSceneManager.OpenScene(ZhongshanBuilder.ScenePath);
            Debug.Log("BLENDER_LANDMARK_SHOTS " + dir);
        }

        // The quad versions off, the models on the same spots.
        static void Swap()
        {
            var env = new GameObject("Blender landmarks").transform;
            // The hills have no collider in the built scene; one for the comparison only, to sit the models on the slope.
            var hills = Find("Hill mesh");
            if (hills && !hills.GetComponent<MeshCollider>()) hills.gameObject.AddComponent<MeshCollider>();
            Physics.SyncTransforms();
            var shrine = Find("Rock shrine (石窟小庙)");
            Hide(shrine, r => r.name == "Cave shrine");
            var cave = BlenderModels.Place(Set, env, "cave_shrine", "Cave shrine (Blender)", new Vector3(93f, 0, 177f));
            // The hill rises from z 176 (heights logged below): steps on the flat, the rock body backed into the slope.
            if (cave) cave.position += new Vector3(0, 0, 170f - BlenderModels.Bounds(cave).min.z);
            Settle(cave);

            var beacon = Find("Old beacon (古烽燧)");
            Hide(beacon, r => r.transform.parent && r.transform.parent.name == "Beacon");
            Settle(BlenderModels.Place(Set, env, "old_beacon", "Old beacon (Blender)", new Vector3(151f, 0, 234f)));

            var pass = Find("井陉口");
            // The picture, its cheval and banner sprites, and the cliff quad standing on the barrier line itself (the
            // model brings its own rock ends).
            Hide(pass, r => r.name == "Black Mountain stockade" || r.name == "Sprite"
                || (r.name == "Cliff" && r.bounds.center.x > 93.5f && Mathf.Abs(r.bounds.center.z - 75f) < 3f));
            // Across the road's end (99, 75) on the flat floor of the gorge; the ground climbs north of z 80.
            var gate = BlenderModels.Place(Set, env, "heishan_stockade", "Black Mountain stockade (Blender)", new Vector3(96f, 0, 72f), -90f);
            if (gate) gate.position += new Vector3(99.5f - BlenderModels.Bounds(gate).max.x, 0, 0);
            Settle(gate);
            foreach (var t in new[] { cave, gate }) if (t) Debug.Log($"BLENDER_LANDMARK {t.name} {BlenderModels.Bounds(t)}");
        }

        // Drops a piece onto the ground under its front edge (the lowest of a few samples along it), so it stands on the
        // slope where the picture stood instead of floating or sinking at y 0.
        static void Settle(Transform t)
        {
            if (!t) return;
            var b = BlenderModels.Bounds(t);
            float low = float.MaxValue;
            foreach (float fx in new[] { 0.2f, 0.5f, 0.8f })
                foreach (float fz in new[] { 0.15f, 0.5f })
                {
                    var from = new Vector3(Mathf.Lerp(b.min.x, b.max.x, fx), 200f, Mathf.Lerp(b.min.z, b.max.z, fz));
                    foreach (var hit in Physics.RaycastAll(from, Vector3.down, 400f))
                        if (!hit.transform.IsChildOf(t) && (hit.collider is MeshCollider || hit.collider.name.Contains("Ground"))) low = Mathf.Min(low, hit.point.y);
                }
            if (low == float.MaxValue) low = 0f;
            t.position = new Vector3(t.position.x, low, t.position.z);
            Debug.Log($"BLENDER_LANDMARK settle {t.name} y {low:0.00}");
        }

        // Height of the hill surface (0 on the plain), from the comparison-only hill collider.
        static float HillY(float x, float z)
        {
            foreach (var hit in Physics.RaycastAll(new Vector3(x, 300f, z), Vector3.down, 600f).OrderBy(h => h.distance))
                if (hit.collider is MeshCollider && hit.collider.name == "Hill mesh") return hit.point.y;
            return 0f;
        }

        static Transform Find(string name) => Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).FirstOrDefault(t => t.name == name);

        static void Hide(Transform root, System.Func<Renderer, bool> which)
        {
            if (!root) { Debug.LogWarning("BLENDER_LANDMARK nothing to hide"); return; }
            foreach (var r in root.GetComponentsInChildren<Renderer>().Where(which)) { r.enabled = false; Debug.Log("BLENDER_LANDMARK hid " + r.name); }
        }

        // Shots are about the landmarks: the walking party and townsfolk would stand in front of them.
        static void HidePeople()
        {
            foreach (string n in new[] { "People", "Whitebox labels" })
            {
                var t = Find(n);
                if (t) t.gameObject.SetActive(false);
            }
        }

        static void Shoot(string file, (string name, Vector3 target, float yaw, float pitch, float distance, float fov) s)
        {
            var rot = Quaternion.Euler(s.pitch, s.yaw, 0f);
            var go = new GameObject("Shot camera");
            var cam = go.AddComponent<Camera>();
            if (Camera.main) cam.CopyFrom(Camera.main);
            go.transform.SetPositionAndRotation(s.target - rot * Vector3.forward * s.distance, rot);
            cam.fieldOfView = s.fov;
            cam.nearClipPlane = 0.5f; cam.farClipPlane = 600f;
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
