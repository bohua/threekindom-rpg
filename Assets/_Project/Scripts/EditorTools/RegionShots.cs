using System.Globalization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ThreeKingdoms.EditorTools
{
    // Dev aid: perspective pictures of a built scene for review, with the scene's post-processing.
    // -shotScene <path> -shots "x,y,z,pitch,yaw,fov,out.png[,nopost];..." (1600 × 900 each).
    public static class RegionShots
    {
        public static void Run()
        {
            var args = System.Environment.GetCommandLineArgs();
            string scene = null, spec = null;
            for (int i = 0; i + 1 < args.Length; i++)
            {
                if (args[i] == "-shotScene") scene = args[i + 1];
                if (args[i] == "-shots") spec = args[i + 1];
            }
            EditorSceneManager.OpenScene(scene);
            // Review pictures keep the colour grade but not the game's depth of field (it follows the player).
            foreach (var v in Object.FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsSortMode.None))
            {
                if (!v.sharedProfile) continue;
                v.sharedProfile = Object.Instantiate(v.sharedProfile);
                if (v.sharedProfile.TryGet<DepthOfField>(out var dof)) dof.active = false;
            }
            foreach (var one in spec.Split(';'))
            {
                var p = one.Split(',');
                float F(int i) => float.Parse(p[i], CultureInfo.InvariantCulture);
                var go = new GameObject("Shot camera");
                var cam = go.AddComponent<Camera>();
                cam.fieldOfView = F(5); cam.nearClipPlane = 0.5f; cam.farClipPlane = 2000f;
                cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = RenderSettings.fogColor;
                // A straight-down shot (pitch 90) is orthographic, fov = metres across; "nopost" skips the depth of field.
                go.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = !(p.Length > 7 && p[7] == "nopost");
                if (F(3) >= 89f) { cam.orthographic = true; cam.orthographicSize = F(5) / 2f * 900f / 1600f; }
                cam.transform.SetPositionAndRotation(new Vector3(F(0), F(1), F(2)), Quaternion.Euler(F(3), F(4), 0));
                var rt = new RenderTexture(1600, 900, 24);
                cam.targetTexture = rt; cam.Render(); cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); tex.Apply();
                System.IO.File.WriteAllBytes(p[6], tex.EncodeToPNG());
                RenderTexture.active = null; cam.targetTexture = null;
                Object.DestroyImmediate(go); Object.DestroyImmediate(rt);
            }
            EditorApplication.Exit(0);
        }
    }
}
