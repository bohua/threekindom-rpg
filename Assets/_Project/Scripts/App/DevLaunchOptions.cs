using System.Collections;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ThreeKingdoms.App
{
    // Development launch switches for builds:
    //   -scene <name>                    load a scene from Build Settings instead of the first one
    //   -camera <object name>            move the main camera to that (disabled) camera's pose and stop following
    //   -teleport <object name>          move the player to that object (e.g. a town "Spot …" marker), camera keeps following
    //   -screenshot <file> [-screenshotDelay <seconds>]   capture one frame, then quit
    //   -panel <shop|save|menu>          open that town panel after loading (screenshots of the menus)
    //   -storyAuto                       play the main line by itself (town, oath, gate, battles); implies -battleAuto
    //   -battleAuto [<stop round>]       let BattleAutoPilot issue commands in battle scenes (screenshots, soak runs)
    public static class DevLaunchOptions
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            // 0.2: -stage <id> picks the story-stage script; -stageAuto <seconds> moves lines on and takes the first choice.
            if (System.Enum.TryParse(LaunchArgs.Get("-chapterStep") ?? "", out ThreeKingdoms.Core.Story.ChapterStep chapterStep))
                ThreeKingdoms.Gameplay.Story.FieldStoryController.DevStep = chapterStep;
            ThreeKingdoms.Gameplay.Story.FieldStoryController.DevTalk = LaunchArgs.Get("-fieldTalk");
            // Screenshot runs keep playing when the window is not in front.
            if (LaunchArgs.Has("-sceneShots") || LaunchArgs.Has("-chapterShots") || LaunchArgs.Has("-comicShots")) Application.runInBackground = true;
            string stageId = LaunchArgs.Get("-stage");
            if (stageId != null) ThreeKingdoms.Gameplay.Story.StageController.DevOverride = stageId;
            if (float.TryParse(LaunchArgs.Get("-stageAuto"), NumberStyles.Float, CultureInfo.InvariantCulture, out float stageAuto))
                ThreeKingdoms.Gameplay.Story.StagePlayer.AutoSeconds = stageAuto;
            // Launches that place the player or the camera themselves skip the first-arrival tour of Zhuojun.
            if (LaunchArgs.Has("-teleport") || LaunchArgs.Has("-camera") || LaunchArgs.Has("-storyAuto") || LaunchArgs.Has("-noIntro"))
                ThreeKingdoms.Gameplay.Story.TownStoryController.PlayArrivalIntro = false;
            string scene = LaunchArgs.Get("-scene");
            if (scene != null && SceneManager.GetActiveScene().name != scene) SceneManager.LoadScene(scene);

            string preset = LaunchArgs.Get("-camera");
            if (preset != null)
            {
                var go = new GameObject(nameof(CameraPresetProbe));
                go.AddComponent<CameraPresetProbe>().PresetName = preset;
                Object.DontDestroyOnLoad(go);
            }

            if (LaunchArgs.Has("-storyAuto"))
            {
                var story = new GameObject(nameof(StoryAutoPilot)).AddComponent<StoryAutoPilot>();
                Object.DontDestroyOnLoad(story.gameObject);
                if (!LaunchArgs.Has("-battleAuto")) Object.DontDestroyOnLoad(new GameObject(nameof(BattleAutoPilot)).AddComponent<BattleAutoPilot>().gameObject);
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

            // -talk notice:3 / tavern:5 / gate2:0 / oath / scroll.taoyuan — play a story scene and advance that many lines (screenshots).
            string talk = LaunchArgs.Get("-talk");
            if (talk != null)
            {
                var go = new GameObject(nameof(TalkProbe));
                var talker = go.AddComponent<TalkProbe>();
                var parts = talk.Split(':');
                talker.Who = parts[0];
                if (parts.Length > 1) int.TryParse(parts[1], out talker.Advances);
                Object.DontDestroyOnLoad(go);
            }

            // -vfx sweep: loop one battle effect for screenshots.
            string vfx = LaunchArgs.Get("-vfx");
            if (vfx != null)
            {
                var go = new GameObject(nameof(VfxProbe));
                go.AddComponent<VfxProbe>().Effect = vfx;
                Object.DontDestroyOnLoad(go);
            }

            string panel = LaunchArgs.Get("-panel");
            if (panel != null)
            {
                var go = new GameObject(nameof(PanelProbe));
                go.AddComponent<PanelProbe>().Panel = panel;
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
            yield return LaunchArgs.WaitForRequestedScene();   // the title is the first scene; let a -scene switch finish
            yield return null;
            yield return null;
            var preset = GameObject.Find(PresetName);
            var main = Camera.main;
            if (!preset || !main) { Debug.LogWarning($"Camera preset '{PresetName}' or main camera not found."); yield break; }
            var follow = main.GetComponent<Jrpg.FollowCamera>();
            if (follow) follow.enabled = false;
            main.transform.SetPositionAndRotation(preset.transform.position, preset.transform.rotation);
            var presetCamera = preset.GetComponent<Camera>();
            if (presetCamera) { main.fieldOfView = presetCamera.fieldOfView; main.farClipPlane = Mathf.Max(main.farClipPlane, presetCamera.farClipPlane); }
            // A whole-town overview (compared side by side with the concept map) skips the haze and the tilt-shift blur.
            if (PresetName.Contains("Overview"))
            {
                RenderSettings.fog = false;
                foreach (var volume in FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsSortMode.None))
                    foreach (var c in volume.profile.components)
                        if (c.GetType().Name == "DepthOfField") c.active = false;
            }
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

    public sealed class PanelProbe : MonoBehaviour
    {
        public string Panel;

        IEnumerator Start()
        {
            yield return LaunchArgs.WaitForRequestedScene();
            yield return new WaitForSecondsRealtime(0.5f);
            var town = Object.FindFirstObjectByType<ThreeKingdoms.Gameplay.Story.TownStoryController>();
            // menu-items / -equip / -skills / -bingshu / -generals / -system: the travel menu on that tab, in the
            // mock-ups' situation (after the first battle: Lv.2, 175 copper, reputation 4, a spare bow; saves go to a
            // temporary folder so the player's own are never touched).
            if (town && Panel.StartsWith("menu-"))
            {
                var run = town.Run;
                run.Story.Join(ThreeKingdoms.Core.Story.StoryState.ZhangFei);
                run.Story.Join(ThreeKingdoms.Core.Story.StoryState.GuanYu);
                if (run.Story.BeginOath()) run.Story.CompleteOath();
                run.Story.RecordVictory("battle_1");
                run.Progress.RestoreExp(run.Progress.Party.Select(g => (g.Id, 10)));
                var inv = run.Progress.Inventory;
                inv.AddCopper(175 - inv.Copper);
                run.Life.AddReputation(4 - run.Life.Reputation);
                inv.AddCopper(inv.Data.Find("horn_bow").BuyPrice); inv.Buy(inv.Data.Find("horn_bow"));
                ThreeKingdoms.Gameplay.Story.GameRun.SaveDirectoryOverride = System.IO.Path.Combine(Application.temporaryCachePath, "menu-shot-saves");
                System.IO.Directory.CreateDirectory(ThreeKingdoms.Gameplay.Story.GameRun.SaveDirectoryOverride);
                run.SaveTo(ThreeKingdoms.Save.SaveSlots.Manual(1), ThreeKingdoms.Save.SaveData.TownLocation);
                run.SaveTo(ThreeKingdoms.Save.SaveSlots.Auto, "battle_2");
                town.OpenPanel(ThreeKingdoms.Gameplay.Story.TownStoryController.MenuPanel);
                yield return null;
                var menu = Object.FindFirstObjectByType<ThreeKingdoms.UI.Menu.MainMenuHud>();
                // "-2" / "-3" after the tab: show the second / third general.
                string tabKey = Panel.Substring(5); int pick = 0;
                if (tabKey.Length > 2 && tabKey[tabKey.Length - 2] == '-' && char.IsDigit(tabKey[tabKey.Length - 1])) { pick = tabKey[tabKey.Length - 1] - '1'; tabKey = tabKey.Substring(0, tabKey.Length - 2); }
                var tab = tabKey switch
                {
                    "equip" => ThreeKingdoms.UI.Menu.MainMenuHud.Tab.Equip, "skills" => ThreeKingdoms.UI.Menu.MainMenuHud.Tab.Skills,
                    "bingshu" => ThreeKingdoms.UI.Menu.MainMenuHud.Tab.Bingshu, "generals" => ThreeKingdoms.UI.Menu.MainMenuHud.Tab.Generals,
                    "system" => ThreeKingdoms.UI.Menu.MainMenuHud.Tab.System, _ => ThreeKingdoms.UI.Menu.MainMenuHud.Tab.Items,
                };
                menu.Show(tab);
                if (pick > 0) menu.SelectGeneral(pick);
                if (tab == ThreeKingdoms.UI.Menu.MainMenuHud.Tab.Equip) menu.PickFor(ThreeKingdoms.Core.Economy.EquipSlot.Weapon);
                yield break;
            }
            if (town && Panel == ThreeKingdoms.Gameplay.Story.TownStoryController.ArcheryPanel) town.OpenArchery();
            else if (town) town.OpenPanel(Panel);
            else Debug.LogWarning("No town to open a panel in.");
        }
    }

    public sealed class VfxProbe : MonoBehaviour
    {
        public string Effect;

        IEnumerator Start()
        {
            yield return LaunchArgs.WaitForRequestedScene();
            ThreeKingdoms.Gameplay.Battle.BattleController b = null;
            while (!(b = Object.FindFirstObjectByType<ThreeKingdoms.Gameplay.Battle.BattleController>()) || b.Session == null) yield return null;
            var vfx = Object.FindFirstObjectByType<ThreeKingdoms.Gameplay.Battle.BattleVfx>();
            while (vfx) { vfx.Preview(Effect); yield return new WaitForSecondsRealtime(0.3f); }
        }
    }

    public sealed class TalkProbe : MonoBehaviour
    {
        public string Who;
        public int Advances;

        IEnumerator Start()
        {
            yield return LaunchArgs.WaitForRequestedScene();
            yield return new WaitForSecondsRealtime(0.5f);
            var town = Object.FindFirstObjectByType<ThreeKingdoms.Gameplay.Story.TownStoryController>();
            if (!town) { Debug.LogWarning("No town to talk in."); yield break; }
            // gate1..gate3: skip ahead to that battle (both brothers joined, oath done, earlier battles won) and face the
            // Yellow Turbans at the south gate.
            if (Who.StartsWith("gate") && int.TryParse(Who.Substring(4), out int battle))
            {
                var story = town.Run.Story;
                story.Join(ThreeKingdoms.Core.Story.StoryState.GuanYu);
                story.Join(ThreeKingdoms.Core.Story.StoryState.ZhangFei);
                if (story.BeginOath()) story.CompleteOath();
                for (int i = 1; i < battle; i++) story.RecordVictory("battle_" + i);
                UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
                yield return null; yield return null;
                town = Object.FindFirstObjectByType<ThreeKingdoms.Gameplay.Story.TownStoryController>();
                // Close enough and the leader challenges by himself; -1 stops short of that to show the squad.
                Teleport(town, new Vector3(0, 0, Advances < 0 ? -35f : -39.6f));
                yield return new WaitForSecondsRealtime(0.5f);
                if (Advances < 0) yield break;
                for (int i = 0; i < Advances; i++) { yield return new WaitForSecondsRealtime(0.4f); town.Advance(); }
                yield break;
            }
            // oath: both brothers in, the peach-garden oath plays by itself (user 2026-10-24).
            if (Who == "oath")
            {
                town.Run.Story.Join(ThreeKingdoms.Core.Story.StoryState.GuanYu);
                town.Run.Story.Join(ThreeKingdoms.Core.Story.StoryState.ZhangFei);
                UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
                yield break;
            }
            // scroll.taoyuan / scroll.huangjin: the ink scroll over the town as it stands.
            if (Who.StartsWith("scroll."))
            {
                town.StartCoroutine(ThreeKingdoms.Gameplay.Story.StoryCinematics.Scroll(Who.Substring(7)));
                yield break;
            }
            // tavern: Zhang Fei already in the party, straight into the tavern scene where Guan Yu joins.
            if (Who == "tavern")
            {
                town.Run.Story.Join(ThreeKingdoms.Core.Story.StoryState.ZhangFei);
                UnityEngine.SceneManagement.SceneManager.LoadScene(ThreeKingdoms.Gameplay.Story.GameRun.TavernScene);
                yield return null; yield return null;
                var stage = Object.FindFirstObjectByType<ThreeKingdoms.Gameplay.Story.TavernStage>();
                for (int i = 0; i < Advances; i++)
                {
                    while (stage && !stage.Line.HasValue) yield return null;
                    yield return new WaitForSecondsRealtime(0.4f);
                    if (stage) stage.Advance();
                    yield return null;
                }
                yield break;
            }
            // notice (default): read the recruiting notice; Zhang Fei walks up and the talk runs that many lines.
            Teleport(town, town.NoticeSpot.position);
            yield return null;
            town.Interact();
            for (int i = 0; i < Advances; i++) { while (town.Busy && !town.Line.HasValue) yield return null; yield return new WaitForSecondsRealtime(0.4f); town.Advance(); yield return null; }
        }

        static void Teleport(ThreeKingdoms.Gameplay.Story.TownStoryController town, Vector3 at)
        {
            var controller = town.Player.GetComponent<CharacterController>();
            if (controller) controller.enabled = false;
            town.Player.transform.position = at;
            if (controller) controller.enabled = true;
        }
    }

    public sealed class TeleportProbe : MonoBehaviour
    {
        public string SpotName;

        IEnumerator Start()
        {
            yield return LaunchArgs.WaitForRequestedScene();   // the title is the first scene; let a -scene switch finish
            yield return null;
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
