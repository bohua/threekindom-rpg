using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ThreeKingdoms.Data.Battle;
using UnityEngine;

namespace ThreeKingdoms.Gameplay.Story
{
    // The story stage scene (V02_HUANGJIN_PLAN §3.2–3.3): marches, the officers' tent (C3), the Guangzong road (C6–C7),
    // Wancheng (C5) and the realm map (C9) are played here, never walked. An orthographic picture like the battles:
    // backgrounds are groups under "Backgrounds" named by id, actors are the character sprites under "Actors" named by
    // their sprite key. GameRun.PendingStage says which script plays.
    public sealed class StageController : MonoBehaviour, IStoryScreen, IStageActors
    {
        public BattleCatalog Catalog;
        public StagePlayer Player;
        public Camera Camera;
        public Transform Backgrounds, Actors;
        [Tooltip("Script played when the scene is opened on its own (development)")] public string DevStage = ChapterScripts.ToYingchuan;

        readonly Dictionary<string, Transform> shown = new Dictionary<string, Transform>();
        Vector3 cameraHome;

        public GameRun Run => GameRun.Current;
        public DialogueLine? Line => Player.Line;
        public IReadOnlyList<string> Choices => Player.Choices;
        public void Choose(int index) => Player.Choose(index);
        public string Card => Player.Card;
        public string Comic => Player.Comic;
        public bool Letterbox => Player.Letterbox;
        public float Fade => Player.Fade;
        public bool Cinematic => Player.Cinematic;
        public StagePlayer.MarchShot March => Player.March;
        public string Objective => null;
        public string Prompt => null;
        public bool Busy => true;
        public string StageId { get; private set; }
        // Development: the script a directly opened stage plays (-stage).
        public static string DevOverride;
        public event Action Changed { add => Player.Changed += value; remove => Player.Changed -= value; }

        void Start()
        {
            if (GameRun.Current == null)
            {
                // Opened on its own: a development run at the chapter step the script expects.
                var run = GameRun.NewGame(Catalog);
                run.PendingStage = DevOverride ?? DevStage;
            }
            if (Camera) cameraHome = Camera.transform.position;
            foreach (Transform b in Backgrounds) b.gameObject.SetActive(false);
            foreach (Transform a in Actors) a.gameObject.SetActive(false);
            StageId = Run.PendingStage ?? DevOverride ?? DevStage;
            Run.PendingStage = null;
            Player.Actors = this;
            StartCoroutine(PlayThenFallback(ChapterScripts.Stage(StageId)));
        }

        IEnumerator PlayThenFallback(IReadOnlyList<StageStep> steps)
        {
            yield return Player.Play(steps);
            // A script that ends without leaving (should not happen) returns to the title rather than a dead screen.
            if (!Player.Left) UnityEngine.SceneManagement.SceneManager.LoadScene(GameRun.TitleScene);
        }

        // ---------- IStageActors ----------

        public void Background(string id)
        {
            foreach (Transform b in Backgrounds) b.gameObject.SetActive(b.name == id);
        }

        static readonly Vector2 Half = new Vector2(10.67f, 6f);

        static Vector3 World(float x, float y) => new Vector3(Mathf.Lerp(-Half.x, Half.x, x), Mathf.Lerp(-Half.y, Half.y, y), 10f + Mathf.Lerp(-Half.y, Half.y, y) * 0.01f);

        public void Show(string id, string sprite, float x, float y, string facing)
        {
            if (!shown.TryGetValue(id, out var t))
            {
                var template = Actors.Find(sprite);
                if (!template) return;
                // The first actor with a sprite uses the template itself; more of the same sprite get copies.
                t = shown.Values.Contains(template) || template.gameObject.activeSelf ? Instantiate(template, Actors) : template;
                shown[id] = t;
            }
            t.gameObject.SetActive(true);
            t.position = World(x, y);
            Face(id, facing);
        }

        public IEnumerator Move(string id, float x, float y, float seconds)
        {
            if (!shown.TryGetValue(id, out var t)) yield break;
            Vector3 from = t.position, to = World(x, y);
            var walk = t.GetComponent<PixelWalkFrames>();
            for (float s = 0; s < seconds; s += Time.deltaTime)
            {
                t.position = Vector3.Lerp(from, to, s / seconds);
                yield return null;
            }
            t.position = to;
        }

        // Pose frames: 0 standing, 1 walking left, 2 walking right, 3 back (the nine-grid order).
        public void Face(string id, string facing)
        {
            if (!shown.TryGetValue(id, out var t)) return;
            var animator = t.GetComponentInChildren<SpriteFrameAnimator>(true);
            if (!animator) return;
            animator.Frame = facing switch { "left" => 1, "right" => 2, "back" => 3, _ => 0 };
            animator.Apply();
        }

        public void Hide(string id) { if (shown.TryGetValue(id, out var t)) t.gameObject.SetActive(false); }

        public void Shake(float seconds) { if (Camera) StartCoroutine(ShakeCamera(seconds)); }

        IEnumerator ShakeCamera(float seconds)
        {
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                Camera.transform.position = cameraHome + (Vector3)(UnityEngine.Random.insideUnitCircle * 0.12f * (1f - t / seconds));
                yield return null;
            }
            Camera.transform.position = cameraHome;
        }

        public void Flash() { }
    }
}
