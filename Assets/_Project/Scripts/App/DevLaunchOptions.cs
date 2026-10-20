using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ThreeKingdoms.App
{
    // Development launch switches for builds:
    //   -scene <name>                    load a scene from Build Settings instead of the first one
    //   -camera <object name>            move the main camera to that (disabled) camera's pose and stop following
    //   -teleport <object name>          move the player to that object (e.g. a town "Spot …" marker), camera keeps following
    //   -screenshot <file> [-screenshotDelay <seconds>]   capture one frame, then quit
    //   -battleAuto [<stop round>]       let BattleAutoPilot issue commands in battle scenes (screenshots, soak runs)
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

            if (LaunchArgs.Has("-battleAuto"))
            {
                var pilot = new GameObject(nameof(BattleAutoPilot)).AddComponent<BattleAutoPilot>();
                if (int.TryParse(LaunchArgs.Get("-battleAuto"), out int stop)) pilot.StopAtRound = stop;
                Object.DontDestroyOnLoad(pilot.gameObject);
            }

            string spot = LaunchArgs.Get("-teleport");
            if (spot != null)
            {
                var go = new GameObject(nameof(TeleportProbe));
                go.AddComponent<TeleportProbe>().SpotName = spot;
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

    public sealed class TeleportProbe : MonoBehaviour
    {
        public string SpotName;

        IEnumerator Start()
        {
            yield return null; // let a -scene switch finish loading
            yield return null;
            var spot = GameObject.Find(SpotName);
            var player = Object.FindFirstObjectByType<Jrpg.PlayerMover>();
            if (!spot || !player) { Debug.LogWarning($"Teleport spot '{SpotName}' or player not found."); yield break; }
            var controller = player.GetComponent<CharacterController>();
            if (controller) controller.enabled = false;
            player.transform.position = spot.transform.position;
            if (controller) controller.enabled = true;
        }
    }
}
