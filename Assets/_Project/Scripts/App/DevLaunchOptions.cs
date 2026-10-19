using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ThreeKingdoms.App
{
    // Development launch switches for builds:
    //   -scene <name>                    load a scene from Build Settings instead of the first one
    //   -camera <object name>            move the main camera to that (disabled) camera's pose and stop following
    //   -screenshot <file> [-screenshotDelay <seconds>]   capture one frame, then quit
    public static class DevLaunchOptions
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            string scene = LaunchArgs.Get("-scene");
            if (scene != null && SceneManager.GetActiveScene().name != scene) SceneManager.LoadScene(scene);

            string preset = LaunchArgs.Get("-camera");
            if (preset != null)
            {
                var go = new GameObject(nameof(CameraPresetProbe));
                go.AddComponent<CameraPresetProbe>().PresetName = preset;
                Object.DontDestroyOnLoad(go);
            }

            string shot = LaunchArgs.Get("-screenshot");
            if (shot == null) return;
            Application.runInBackground = true;
            var probe = new GameObject(nameof(ScreenshotProbe)).AddComponent<ScreenshotProbe>();
            probe.Path = shot;
            string delay = LaunchArgs.Get("-screenshotDelay");
            if (delay != null) probe.Delay = float.Parse(delay, CultureInfo.InvariantCulture);
            Object.DontDestroyOnLoad(probe.gameObject);
        }
    }

    public sealed class CameraPresetProbe : MonoBehaviour
    {
        public string PresetName;

        IEnumerator Start()
        {
            yield return null; // let a -scene switch finish loading
            yield return null;
            var preset = GameObject.Find(PresetName);
            var main = Camera.main;
            if (!preset || !main) { Debug.LogWarning($"Camera preset '{PresetName}' or main camera not found."); yield break; }
            var follow = main.GetComponent<Jrpg.FollowCamera>();
            if (follow) follow.enabled = false;
            main.transform.SetPositionAndRotation(preset.transform.position, preset.transform.rotation);
            var presetCamera = preset.GetComponent<Camera>();
            if (presetCamera) main.fieldOfView = presetCamera.fieldOfView;
        }
    }

    public sealed class ScreenshotProbe : MonoBehaviour
    {
        public string Path;
        public float Delay = 4f;

        IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(Delay);
            yield return new WaitForEndOfFrame();
            // The ScreenCapture module is not enabled in this project; read the back buffer instead.
            var frame = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            frame.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            frame.Apply();
            System.IO.File.WriteAllBytes(Path, frame.EncodeToPNG());
            Destroy(frame);
            Application.Quit();
        }
    }
}
