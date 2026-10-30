using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Jrpg;
using ThreeKingdoms.Core.Story;
using ThreeKingdoms.Data.Battle;
using ThreeKingdoms.Save;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ThreeKingdoms.Gameplay.Story
{
    // The walkable 0.2 places (V02_HUANGJIN_PLAN §3.1, §6.2): Lu Zhi's camp at Guangzong and Anxi county. Liu Bei walks with
    // his brothers behind him; people stand at their doors and talk when he comes up to them (confirm); the gate guard
    // and the county clerk keep the manual save, the quartermaster and the shopkeepers trade. What each person says and
    // does is FieldScripts; cutscenes play through the StagePlayer like on the story stage.
    public sealed class FieldStoryController : MonoBehaviour, IStoryScreen, IStageActors, IFieldHost, IShopTier, IFieldMarks
    {
        public enum Place { Camp, Anxi, Gongsun, Suanzao, Pingyuan, Xiapi }   // 0.3: Gongsun Zan's garrison, the Suanzao camp (FieldScripts3)

        public Place Location;
        public BattleCatalog Catalog;
        public PlayerMover Player;
        public FollowCamera Camera;
        public Camera View;
        public PartyFollower GuanYuFollower, ZhangFeiFollower;
        public InputActionReference InteractAction, MenuAction;
        public StagePlayer Stage;
        [Tooltip("Everyone Liu Bei can talk to, named \"<id> <label>\" (FieldScripts ids)")] public Transform[] People = Array.Empty<Transform>();
        [Tooltip("Places that react without a person (a gate, a timber pile, a hidden keepsake), named \"<id> <label>\"")] public Transform[] Spots = Array.Empty<Transform>();
        public Transform ArrivalSpot, ReturnSpot;
        [Tooltip("Things switched on or off by flags: \"<flag>\" shows the object once the flag is set, \"!<flag>\" until then")]
        public Transform[] FlagObjects = Array.Empty<Transform>();
        public float TalkRange = 2.4f;

        public GameRun Run { get; private set; }
        public bool Busy { get; private set; }
        public string Prompt { get; private set; }
        public string Panel { get; private set; }
        public event Action<string> PanelOpened, PanelClosed;
        public event Action Changed;

        public DialogueLine? Line => Stage.Line;
        public IReadOnlyList<string> Choices => Stage.Choices;
        public void Choose(int index) => Stage.Choose(index);
        public string Card => Stage.Card;
        public string Comic => Stage.Comic;
        public bool Letterbox => Stage.Letterbox;
        public float Fade => Stage.Fade;
        public bool Cinematic => Stage.Cinematic;
        public StagePlayer.MarchShot March => Stage.March;
        public string Objective => Run == null ? null : ObjectiveText();
        BattleCatalog IFieldHost.Catalog => Catalog;
        public string SaveLocation => Location switch
        {
            Place.Camp => SaveData.CampLocation, Place.Gongsun => SaveData.GongsunLocation, Place.Suanzao => SaveData.SuanzaoLocation,
            Place.Pingyuan => FieldScripts4.RegionLocation, Place.Xiapi => SaveData.XiapiLocation, _ => SaveData.AnxiLocation,
        };
        public int ShopTier => Location switch { Place.Anxi => 2, Place.Gongsun or Place.Suanzao or Place.Pingyuan or Place.Xiapi => 3, _ => 1 };
        public Camera MarkCamera => View;
        public IEnumerable<Transform> Marks => Run == null || Busy ? Enumerable.Empty<Transform>()
            : People.Concat(Spots).Where(t => t && t.gameObject.activeInHierarchy && FieldScripts.Marked(this, IdOf(t)));
        public Func<string> ObjectiveText = () => null;

        int closedFrame = -1, openedFrame = -1;
        public static string IdOf(Transform t) => t ? t.name.Split(' ')[0] : null;
        public Transform Find(string id) => People.Concat(Spots).FirstOrDefault(t => t && IdOf(t) == id);

        void OnEnable()
        {
            if (InteractAction) InteractAction.action.Enable();
            if (MenuAction) MenuAction.action.Enable();
        }

        void Start()
        {
            Run = GameRun.Current ?? DevRun();
            Stage.Actors = this;
            Stage.Changed += () => Changed?.Invoke();
            var spot = Run.NextArrival == GameRun.Arrival.ReturnFromBattle && ReturnSpot && (Location == Place.Anxi && Run.Chapter2.Step == ChapterStep.AnxiBandits || Location == Place.Gongsun || Location == Place.Suanzao || Location == Place.Pingyuan || Location == Place.Xiapi)
                ? ReturnSpot : ArrivalSpot;
            var gate = RegionPortal.TakeArrival(out _);   // back in through a gate from the open region (R2 中山)
            if (gate) spot = gate;
            Run.NextArrival = GameRun.Arrival.NewGame;
            PlaceAt(Player.transform, spot ? spot.position : Player.transform.position);
            ShowFollowers();
            RefreshFlags();
            ObjectiveText = () => FieldScripts.Objective(this);
            var arrival = FieldScripts.OnArrive(this);
            if (arrival != null) Play(arrival);
            if (DevTalk != null && Run.IsDevelopment) StartCoroutine(DevTalkRoutine());
            Changed?.Invoke();
        }

        // Opened on its own (development): a run at this place's step.
        GameRun DevRun()
        {
            if (DevStep == null && Location == Place.Xiapi)
                return GameRun.ForChapter(Catalog, ChapterStep.Xiapi, 810, 900, 2, development: true);
            if (Location == Place.Pingyuan)
                return GameRun.ForChapter(Catalog, ChapterStep.Pingyuan, 370, 500, 2, development: true);
            if (Location == Place.Gongsun || Location == Place.Suanzao)
                return GameRun.ForChapter(Catalog, Location == Place.Gongsun ? ChapterStep.Gongsun : ChapterStep.Suanzao, 210, 400, 2, development: true);
            var step = DevStep ?? (Location == Place.Camp ? ChapterStep.Camp : ChapterStep.Anxi);
            return GameRun.ForChapter(Catalog, step, Location == Place.Camp ? 60 : step >= ChapterStep.AnxiBandits ? 210 : 150, 260, 2, development: true);
        }

        // Development (-chapterStep, -fieldTalk): open at a later step and talk to someone once the arrival scene is over.
        public static ChapterStep? DevStep;
        public static string DevTalk;

        System.Collections.IEnumerator DevTalkRoutine()
        {
            while (Busy) yield return null;
            yield return new WaitForSeconds(0.5f);
            var who = Find(DevTalk);
            if (!who) yield break;
            PlaceAt(Player.transform, who.position + new Vector3(0, 0, -1.4f));
            yield return null; yield return null;
            Interact();
        }

        void Update()
        {
            bool menu = MenuAction && MenuAction.action.WasPressedThisFrame();
            if (Panel == TownStoryController.MenuPanel && menu && Time.frameCount > openedFrame) { ClosePanel(); return; }
            if (Panel != null || Time.frameCount <= closedFrame + 1) return;
            if (Busy) return;   // the StagePlayer takes confirm while a scene plays
            if (menu) { OpenPanel(TownStoryController.MenuPanel); return; }
            UpdatePrompt();
            if (InteractAction && InteractAction.action.WasPressedThisFrame()) Interact();
        }

        void UpdatePrompt()
        {
            var p = Player.transform.position;
            string prompt = null;
            var near = People.Concat(Spots).Where(t => t && t.gameObject.activeInHierarchy && Flat(t.position - p).magnitude <= TalkRange)
                .OrderBy(t => Flat(t.position - p).magnitude).FirstOrDefault();
            if (near) prompt = FieldScripts.Prompt(this, IdOf(near));
            if (prompt == Prompt) return;
            Prompt = prompt;
            Changed?.Invoke();
        }

        public void Interact()
        {
            if (Busy || Panel != null) return;
            var p = Player.transform.position;
            var near = People.Concat(Spots).Where(t => t && t.gameObject.activeInHierarchy && Flat(t.position - p).magnitude <= TalkRange)
                .OrderBy(t => Flat(t.position - p).magnitude).FirstOrDefault();
            if (!near) return;
            string id = IdOf(near);
            Face(near);
            var steps = FieldScripts.Talk(this, id);
            if (steps != null) Play(steps);
        }

        // Plays a scene; movement stops meanwhile, flags and followers are refreshed after.
        public void Play(IEnumerable<StageStep> steps) => StartCoroutine(PlayScene(steps));

        IEnumerator PlayScene(IEnumerable<StageStep> steps)
        {
            Busy = true;
            Prompt = null;
            SetControl(false);
            Changed?.Invoke();
            try { yield return Stage.Play(steps); }
            finally
            {
                if (!Stage.Left)
                {
                    foreach (var f in staged) if (f) f.enabled = true;
                    staged.Clear();
                    ShowFollowers();
                    RefreshFlags();
                    Busy = false;
                    SetControl(true);
                }
                Changed?.Invoke();
            }
            if (!Stage.Left && !string.IsNullOrEmpty(pendingPanel)) { var panel = pendingPanel; pendingPanel = null; OpenPanel(panel); }
        }

        // A scene can end by opening the shop or the save panel (FieldScripts sets this).
        [NonSerialized] public string pendingPanel;

        // Shows or hides what the flags say (the broken / mended bridge, the timber pile, the ox at the trough …).
        public void RefreshFlags()
        {
            foreach (var t in People)
                if (t && SideQuests.Present(this, IdOf(t)) is bool shown) t.gameObject.SetActive(shown);
            foreach (var t in FlagObjects)
            {
                if (!t) continue;
                string flag = t.name.Split(' ')[0];
                bool not = flag.StartsWith("!");
                bool has = Run.Chapter2.Has(not ? flag.Substring(1) : flag);
                t.gameObject.SetActive(not ? !has : has);
            }
        }

        // ---------- panels (IFieldHost) ----------

        public void OpenPanel(string panel)
        {
            if (Panel != null) return;
            Panel = panel;
            Busy = true;
            openedFrame = Time.frameCount;
            SetControl(false);
            Prompt = null;
            PanelOpened?.Invoke(panel);
            Changed?.Invoke();
        }

        public void ClosePanel()
        {
            if (Panel == null) return;
            string closed = Panel;
            Panel = null;
            Busy = false;
            closedFrame = Time.frameCount;
            SetControl(true);
            PanelClosed?.Invoke(closed);
            Changed?.Invoke();
        }

        public void SwitchPanel(string panel) { ClosePanel(); closedFrame = -1; OpenPanel(panel); }

        public void ReturnToTitle() { GameRun.Clear(); SceneManager.LoadScene(GameRun.TitleScene); }

        // ---------- IStageActors: in a walkable scene x and y are world X and Z ----------

        readonly List<PartyFollower> staged = new List<PartyFollower>();

        Transform Actor(string id) => id == "liu_bei" ? Player.transform
            : id == "guan_yu" ? (GuanYuFollower ? GuanYuFollower.transform : null)
            : id == "zhang_fei" ? (ZhangFeiFollower ? ZhangFeiFollower.transform : null)
            : Find(id);

        public void Background(string id) { }

        public void Show(string id, string sprite, float x, float y, string facing)
        {
            var t = Actor(id);
            if (!t) return;
            Pin(t);
            t.gameObject.SetActive(true);
            if (x > -900f) PlaceAt(t, new Vector3(x, t.position.y, y));
            Face(id, facing);
        }

        void Pin(Transform t)
        {
            var f = t.GetComponent<PartyFollower>();
            if (f && f.enabled) { f.enabled = false; staged.Add(f); }
        }

        public IEnumerator Move(string id, float x, float y, float seconds)
        {
            var t = Actor(id);
            if (!t) yield break;
            Pin(t);
            var controller = t.GetComponent<CharacterController>();
            if (controller) controller.enabled = false;
            var to = new Vector3(x, t.position.y, y);
            float speed = Mathf.Max(0.5f, Flat(to - t.position).magnitude / Mathf.Max(0.1f, seconds));
            try { yield return StageMoves.Walk(t, new[] { to }, speed); }
            finally { if (controller) controller.enabled = true; }
        }

        public void Face(string id, string facing)
        {
            var t = Actor(id);
            if (!t) return;
            var dir = facing switch { "left" => Vector3.left, "right" => Vector3.right, "back" => Vector3.forward, _ => Vector3.back };
            var walk = t.GetComponent<PixelWalkFrames>();
            if (walk) { walk.Face(dir); return; }
            var animator = t.GetComponentInChildren<SpriteFrameAnimator>();
            if (!animator) return;
            animator.Frame = facing switch { "left" => 1, "right" => 2, "back" => 3, _ => 0 };
            animator.Apply();
        }

        public void Hide(string id) { var t = Actor(id); if (t && t != Player.transform) { Pin(t); t.gameObject.SetActive(false); } }

        // 鞭督邮 story poses (DuyouPoseImporter): the brothers' atlases hold them as story poses 6–13 after the oath's six,
        // the inspector's after his nine standing poses.
        public const int HeroDuyouPose = 6, NpcDuyouFrame = 9;

        public void Pose(string id, int pose, bool flip)
        {
            var t = Actor(id);
            if (!t) return;
            var walk = t.GetComponent<PixelWalkFrames>();
            if (walk)
            {
                walk.PoseOverride = pose < 0 ? -1 : HeroDuyouPose + pose;
                walk.PoseFlip = flip;
                return;
            }
            var animator = t.GetComponentInChildren<SpriteFrameAnimator>();
            if (!animator) return;
            int frame = pose < 0 ? 0 : NpcDuyouFrame + pose;
            if (frame < animator.Columns * animator.Rows) { animator.Frame = frame; animator.Apply(); }
            var board = animator.GetComponent<PixelBillboard>();
            if (board) board.Flip = flip;
        }

        Transform focus;

        public void Focus(float x, float z, float distance)
        {
            if (!Camera) return;
            if (distance <= 0f)
            {
                Camera.StageDistance = 0f;
                Camera.Target = Player.transform;
                return;
            }
            if (!focus) focus = new GameObject("Stage focus").transform;
            focus.position = new Vector3(x, Player.transform.position.y, z);
            Camera.Target = focus;
            Camera.StageDistance = distance;
        }

        public void Shake(float seconds) { if (Camera) Camera.ShakeSeconds = seconds; }
        public void Flash() { }

        // A standing person turns to Liu Bei (pose frames 0 front, 1 left, 2 right, 3 back).
        void Face(Transform who)
        {
            if (!who || who.GetComponent<PixelWalkFrames>()) return;
            var animator = who.GetComponentInChildren<SpriteFrameAnimator>();
            if (!animator) return;
            Vector3 d = Player.transform.position - who.position;
            animator.Frame = Mathf.Abs(d.x) > Mathf.Abs(d.z) ? (d.x < 0f ? 1 : 2) : (d.z > 0f ? 3 : 0);
            animator.Apply();
        }

        public void ShowFollowers()
        {
            var trail = Player ? Player.GetComponent<PartyTrail>() : null;
            if (trail) trail.Reset(Vector3.back);
            int slot = 0;
            foreach (var f in new[] { GuanYuFollower, ZhangFeiFollower })
            {
                if (!f) continue;
                f.gameObject.SetActive(true);
                f.enabled = true;
                f.Slot = ++slot;
                f.Trail = trail;
                f.Snap();
            }
        }

        void SetControl(bool on) { if (Player) Player.InputEnabled = on; }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);

        static void PlaceAt(Transform who, Vector3 at)
        {
            var controller = who.GetComponent<CharacterController>();
            if (controller) controller.enabled = false;
            who.position = at;
            if (controller) controller.enabled = true;
        }
    }

    // Someone worth a "!" over the head (a pending story beat or an unfinished commission).
    public interface IFieldMarks
    {
        Camera MarkCamera { get; }
        IEnumerable<Transform> Marks { get; }
    }
}
