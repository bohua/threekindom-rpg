using System.Globalization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ThreeKingdoms.EditorTools
{
    // Dev aid: a straight-down orthographic picture of part of the Bohai plain, for checking ground seams and road
    // shapes. -topShot x,z,size,outfile.png (several separated by ';').
    public static class BohaiTopShot
    {
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Bohai_Plain.unity");
            var args = System.Environment.GetCommandLineArgs();
            string spec = null;
            for (int i = 0; i + 1 < args.Length; i++) if (args[i] == "-topShot") spec = args[i + 1];
            foreach (var one in spec.Split(';'))
            {
                var p = one.Split(',');
                float x = float.Parse(p[0], CultureInfo.InvariantCulture), z = float.Parse(p[1], CultureInfo.InvariantCulture), size = float.Parse(p[2], CultureInfo.InvariantCulture);
                var go = new GameObject("Top shot camera");
                var cam = go.AddComponent<Camera>();
                cam.orthographic = true; cam.orthographicSize = size / 2f;
                cam.transform.SetPositionAndRotation(new Vector3(x, 80f, z), Quaternion.Euler(90f, 0, 0));
                cam.nearClipPlane = 1f; cam.farClipPlane = 200f;
                var rt = new RenderTexture(1200, 1200, 24);
                cam.targetTexture = rt; cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(1200, 1200, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 1200, 1200), 0, 0); tex.Apply();
                System.IO.File.WriteAllBytes(p[3], tex.EncodeToPNG());
                RenderTexture.active = null; cam.targetTexture = null;
                Object.DestroyImmediate(go); Object.DestroyImmediate(rt);
            }
            EditorApplication.Exit(0);
        }
    }
}
