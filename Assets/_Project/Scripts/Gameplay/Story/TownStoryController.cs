using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Jrpg;
using ThreeKingdoms.Core.Economy;
using ThreeKingdoms.Core.Story;
using ThreeKingdoms.Data.Battle;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ThreeKingdoms.Gameplay.Story
{
    // One line of dialogue or narration; the UI turns the keys into text (StoryText), so this layer stays text-free.
    public readonly struct DialogueLine
    {
        public readonly string Speaker, Line;
        public DialogueLine(string speaker, string line) { Speaker = speaker; Line = line; }
    }

    // Drives the Zhuojun main line (ADR-0006 §3, story-events GDD): talk to Guan Yu and Zhang Fei to recruit them,
    // the automatic peach-garden oath once both have joined, and leaving by the south gate for the next battle.
    // Rules come from StoryState; this component only stages them.
    public sealed class TownStoryController : MonoBehaviour, IPanelHost
    {
        public BattleCatalog Catalog;
        public PlayerMover Player;
        public FollowCamera Camera;
        public Transform GuanYu, ZhangFei;
        [Tooltip("Companions walking behind Liu Bei once they have joined (user 2026-10-20)")]
        public PartyFollower GuanYuFollower, ZhangFeiFollower;
        public InputActionReference InteractAction, MenuAction;
        [Tooltip("N04 铁匠兼装备商：在此对话打开武器装备店（equipment-items GDD）")]
        public Transform Smith;
        [Tooltip("N05 酒馆掌柜：在此对话打开手动存档（save-system GDD answer 1）")]
        public Transform Innkeeper;
        [Tooltip("The town residents N01–N12 (ZHUOJUN_TOWN_DESIGN.md §4); N04 and N05 open the shop and the save panel")]
        public Transform[] Residents = Array.Empty<Transform>();
        [Header("Meeting Zhang Fei and Guan Yu (user 2026-10-22)")]
        [Tooltip("The notice board on the market square: reading it starts the meeting with Zhang Fei")] public Transform NoticeBoard;
        [Tooltip("Where Liu Bei stands to read it")] public Transform NoticeSpot;
        [Tooltip("Zhang Fei's walk in from off screen: the first point is where he appears")] public Vector3[] ZhangFeiEntrance = Array.Empty<Vector3>();
        [Tooltip("The tavern door: with Zhang Fei in the party, entering leads to the tavern scene where Guan Yu joins")] public Transform TavernDoor;
        [Header("Spots")]
        public Transform NewGameSpot, ReturnSpot, OathEndSpot, GroveFocus, TavernSpot;
        public Transform[] GroveSpots = Array.Empty<Transform>();   // Liu, Guan, Zhang
        [Header("South gate passage")]
        public Vector3 GateCenter = new Vector3(0, 1, -36f);
        public Vector3 GateSize = new Vector3(4f, 3f, 5f);
        [Tooltip("Yellow Turbans blocking the south gate, one group per battle (user 2026-10-22); the first child is the leader who talks")]
        public Transform[] GateBlockers = Array.Empty<Transform>();
        [Tooltip("Keeps Liu Bei from walking past the blockers")] public Collider GateWall;
        [Tooltip("Within this distance of the leader at the gate the challenge starts by itself (user 2026-10-22)")]
        public float ChallengeRange = 3.2f;

        // A city gate with two guards (user 2026-10-22): Liu Bei may come in but not go out; walking into the passage
        // from inside town, the guards stop him and send him back. The south guards stand down once Guan Yu and Zhang
        // Fei have both joined, and the Yellow Turbans take their place outside.
        [Serializable]
        public struct CityGate
        {
            public string Id;               // "south", "west"
            public Vector3 Center, Size;    // the passage through the wall
            public Vector3 Inward;          // towards the town
            public Transform Guards;
        }
        [Header("Gate guards")]
        public CityGate[] Gates = Array.Empty<CityGate>();
        [Tooltip("Inside the walls; once Liu Bei has been here, a gate passage counts as a way out")]
        public Bounds TownInside = new Bounds(Vector3.zero, new Vector3(90f, 20f, 66f));
        bool wasInside;
        public bool WasInside => wasInside;
        public float TalkRange = 2.2f;
        [Tooltip("Residents within this distance say their line in a bubble, no button needed (user 2026-10-21)")]
        public float BubbleRange = 3f;
        [Tooltip("Seconds for a fade to black / back")] public float FadeTime = 0.4f;

        public GameRun Run { get; private set; }
        public bool Busy { get; private set; }
        public DialogueLine? Line { get; private set; }
        // "talk:guan_yu", "gate:locked", "gate:battle_2" … or null.
        public string Prompt { get; private set; }
        public float Fade { get; private set; }
        public bool InGatePassage { get; private set; }
        // The options of a choice on screen (StoryText keys), or null; Choose(i) answers it.
        public IReadOnlyList<string> Choices { get; private set; }
        int? chosen;
        public void Choose(int index) { if (Choices != null && index >= 0 && index < Choices.Count) chosen = index; }

        public const string ShopPanel = "shop", SavePanel = "save", OutfitPanel = "outfit", MenuPanel = "menu";
        // The full-screen panel that owns input right now (shop or save), or null.
        public string Panel { get; private set; }
        public bool ShopOpen => Panel == ShopPanel;
        public event Action<string> PanelOpened, PanelClosed;

        public event Action Changed;

        // ---------- bubbles (user 2026-10-21) ----------
        // Residents near Liu Bei speak in a bubble over their heads; it disappears when he walks away.
        readonly List<Transform> speaking = new List<Transform>();
        public IReadOnlyList<Transform> Speaking => speaking;
        // Story characters whose event is still pending wear a "!" until it is done (Guan Yu, Zhang Fei before joining).
        public IEnumerable<Transform> StoryMarks
        {
            get
            {
                if (Run == null || Busy) yield break;
                // The notice board until Zhang Fei has joined, then the tavern door until Guan Yu has (user 2026-10-22).
                if (NoticeBoard && !Run.Story.ZhangFeiJoined) yield return NoticeBoard;
                else if (TavernDoor && Run.Story.ZhangFeiJoined && !Run.Story.GuanYuJoined) yield return TavernDoor;
                if (BlockerLeader) yield return BlockerLeader;
            }
        }

        // The leader of the Yellow Turbans now at the south gate, or null when none block it.
        public Transform BlockerLeader
        {
            get
            {
                var group = ActiveBlockers;
                return group && group.childCount > 0 ? group.GetChild(0) : null;
            }
        }
        Transform ActiveBlockers => GateBlockers.FirstOrDefault(g => g && g.gameObject.activeSelf);
        // Liu Bei stands where something happens (an event spot such as the south gate): "!" over his head.
        public bool PlayerAlert => !Busy && InGatePassage && !BlockerLeader;

        public string ResidentLineKey(string id)
        {
            var story = Run.Story;
            string stage = story.BattlesDone > 0 ? ".won" : story.Oath == SequenceState.Done ? ".after" : "";
            return "npc." + id + stage;
        }

        bool advance;
        string pendingGateScene;
        int closedShopFrame = -1, openedPanelFrame = -1;
        bool warnedNoWeapon;

        // 演义 第1回: Liu Bei sighs before the recruiting notice and Zhang Fei calls out to him (user 2026-10-22).
        static readonly DialogueLine[] NoticeRead =
        {
            new DialogueLine("narrator", "notice.1"), new DialogueLine("liu_bei", "notice.2"),
        };
        static readonly DialogueLine[] ZhangFeiCallsOut =
        {
            new DialogueLine("zhang_fei", "zhang.1"), new DialogueLine("liu_bei", "zhang.2"),
            new DialogueLine("zhang_fei", "zhang.3"),
        };
        public static readonly string[] ZhangFeiChoices = { "choice.serve", "choice.wait" };
        static readonly DialogueLine[][] ZhangFeiAnswers =
        {
            new[] { new DialogueLine("liu_bei", "zhang.serve.1"), new DialogueLine("zhang_fei", "zhang.serve.2") },
            new[] { new DialogueLine("liu_bei", "zhang.wait.1"), new DialogueLine("zhang_fei", "zhang.wait.2"), new DialogueLine("liu_bei", "zhang.wait.3") },
        };
        static readonly DialogueLine[] ZhangFeiJoins =
        {
            new DialogueLine("system", "zhang.joined"), new DialogueLine("zhang_fei", "zhang.tavern"),
        };
        static readonly DialogueLine[] OathScene =
        {
            new DialogueLine("narrator", "oath.1"), new DialogueLine("zhang_fei", "oath.2"),
            new DialogueLine("liu_bei", "oath.3"), new DialogueLine("guan_yu", "oath.4"),
            new DialogueLine("narrator", "oath.5"),
        };

        void OnEnable()
        {
            if (InteractAction) InteractAction.action.Enable();
            if (MenuAction) MenuAction.action.Enable();
        }

        void OnDisable()
        {
            if (InteractAction) InteractAction.action.Disable();
            if (MenuAction) MenuAction.action.Disable();
        }

        void Start()
        {
            Run = GameRun.Current ?? GameRun.NewGame(Catalog);
            var story = Run.Story;
            Place(Player.transform, Run.NextArrival switch
            {
                GameRun.Arrival.ReturnFromBattle => ReturnSpot,
                GameRun.Arrival.AtTavern => TavernSpot ? TavernSpot : ReturnSpot,
                GameRun.Arrival.FromTavernInside => TavernSpot ? TavernSpot : ReturnSpot,
                _ => NewGameSpot,
            });
            Run.NextArrival = GameRun.Arrival.ReturnFromBattle;   // any later town load is a return
            // Neither stands in town any more: Zhang Fei walks up at the notice board, Guan Yu is met in the tavern.
            if (GuanYu) GuanYu.gameObject.SetActive(false);
            if (ZhangFei) ZhangFei.gameObject.SetActive(false);
            ShowFollowers(Vector3.back);   // arriving through the south gate: the party trails into the passage
            ShowBlockers();
            wasInside = TownInside.Contains(Player.transform.position);
            SetControl(true);
            // An oath interrupted by a reload replays from the start (GDD rule 11).
            story.ToSafeState();
            if (story.Phase == StoryPhase.Oath) StartCoroutine(PlayOath());
            Changed?.Invoke();
        }

        void Update()
        {
            UpdateBubbles();
            // Residents stop while a conversation or panel holds the town, and while they speak to Liu Bei.
            foreach (var r in Residents)
                if (r && r.TryGetComponent<NpcPatrol>(out var patrol)) patrol.Paused = Busy || speaking.Contains(r);
            bool menuPressed = MenuAction && MenuAction.action.WasPressedThisFrame();
            // The menu key also closes the main menu; other panels own input until they close.
            if (Panel == MenuPanel && menuPressed && Time.frameCount > openedPanelFrame) { ClosePanel(); return; }
            if (Panel != null) return;
            // The press that closed a panel (Cancel / confirm / menu) must not reopen one.
            if (Time.frameCount <= closedShopFrame + 1) return;
            // Main menu: 武将, 物品, 设置 (game-ui answer 10); not during the oath or dialogue.
            if (menuPressed && !Busy) { OpenPanel(MenuPanel); return; }
            bool pressed = InteractAction && InteractAction.action.WasPressedThisFrame();
            if (Busy) { if (pressed) Advance(); return; }
            if (CheckGates()) return;
            UpdatePrompt();
            if (pressed) Interact();
        }

        // ---------- public entry points (input and tests) ----------

        public void Advance() { if (Busy) advance = true; }

        public void Interact()
        {
            if (Busy) return;
            UpdatePrompt();
            if (Prompt == null) return;
            if (Prompt == "notice") StartCoroutine(Run.Story.ZhangFeiJoined ? Say(NoticeRead) : MeetZhangFei());
            else if (Prompt == "tavern") StartCoroutine(EnterTavern());
            else if (Prompt == "gate:locked" || Prompt == "gate:done") StartCoroutine(Say(new[] { new DialogueLine("system", Prompt) }));
            else if (Prompt == ShopPanel || Prompt == SavePanel) { Face(Prompt == ShopPanel ? Smith : Innkeeper); OpenPanel(Prompt); }
            else if (Prompt.StartsWith("npc:")) TalkToResident(Prompt.Substring(4));
            else if (Prompt.StartsWith("gate:battle_")) ChallengeAtGate();
        }

        void ChallengeAtGate()
        {
            // A general without a weapon may still fight; warn once per visit (equipment-items GDD answer 2), and step
            // back from the Yellow Turbans so there is time to go and arm him.
            var bare = Run.Data.Party.Where(g => Run.Story.HasJoined(g.Id) && Run.Progress.Inventory.Worn(g.Id, EquipSlot.Weapon) == null).ToList();
            if (bare.Count > 0 && !warnedNoWeapon)
            {
                warnedNoWeapon = true;
                StartCoroutine(WarnUnarmed("warn:no_weapon:" + string.Join("、", bare.Select(g => g.DisplayName))));
            }
            else StartCoroutine(Confront());
        }

        IEnumerator WarnUnarmed(string line)
        {
            yield return Say(new[] { new DialogueLine("system", line) });
            var back = new GameObject("step back").transform;
            back.position = new Vector3(GateCenter.x, 0, GateCenter.z + GateSize.z / 2f + 1.6f);
            Place(Player.transform, back);
            Destroy(back.gameObject);
            ShowFollowers(Vector3.back);
            Changed?.Invoke();
        }

        // Shop (N04) or manual save (N05): movement pauses while the panel is open (town-exploration GDD).
        public void OpenPanel(string panel)
        {
            if (Busy) return;
            Busy = true;
            Panel = panel;
            openedPanelFrame = Time.frameCount;
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
            warnedNoWeapon = false;   // gear may have changed: warn again if still unarmed
            closedShopFrame = Time.frameCount;
            SetControl(true);
            PanelClosed?.Invoke(closed);
            Changed?.Invoke();
        }

        public void OpenShop() => OpenPanel(ShopPanel);

        // ---------- residents ----------

        // "N03 城主府掌士" → "N03".
        public static string ResidentId(Transform resident) => resident.name.Split(' ')[0];

        public Transform Resident(string id) => Residents.FirstOrDefault(r => r && ResidentId(r) == id);

        // One useful line, switching after the oath and again after the first victory (design §4); the UI falls back
        // to the earlier line when a resident has nothing new to say.
        public void TalkToResident(string id)
        {
            var who = Resident(id);
            if (!who || Busy) return;
            Face(who);
            var story = Run.Story;
            string stage = story.BattlesDone > 0 ? ".won" : story.Oath == SequenceState.Done ? ".after" : "";
            StartCoroutine(Say(new[] { new DialogueLine("npc." + id, "npc." + id + stage) }));
        }

        // A standing resident turns to Liu Bei (pose frames: 0 front, 1 left, 2 right, 3 back).
        void Face(Transform who)
        {
            if (!who || who.GetComponent<PixelWalkFrames>()) return;   // walkers keep their walk facing
            var animator = who.GetComponentInChildren<SpriteFrameAnimator>();
            if (!animator) return;
            Vector3 d = Player.transform.position - who.position;
            animator.Frame = Mathf.Abs(d.x) > Mathf.Abs(d.z) ? (d.x < 0f ? 1 : 2) : (d.z > 0f ? 3 : 0);
            animator.Apply();
        }

        // From the main menu: one panel at a time, so the menu closes before the outfit panel opens.
        public void SwitchPanel(string panel)
        {
            ClosePanel();
            closedShopFrame = -1;
            OpenPanel(panel);
        }

        public void ReturnToTitle()
        {
            GameRun.Clear();
            SceneManager.LoadScene(GameRun.TitleScene);
        }
        public void CloseShop() => ClosePanel();

        // ---------- prompts ----------

        void UpdatePrompt()
        {
            string prompt = null;
            var story = Run.Story;
            var p = Player.transform.position;
            InGatePassage = new Bounds(GateCenter, GateSize).Contains(p + Vector3.up);
            if (InGatePassage || (Near(BlockerLeader, p) && story.CanLeaveTown(out _)))
                prompt = story.CanLeaveTown(out string reason) ? "gate:" + story.NextBattleId
                    : reason == StoryState.ReasonAllDone ? "gate:done" : "gate:locked";
            else if (Near(NoticeBoard, p)) prompt = "notice";
            else if (Near(TavernDoor, p) && story.ZhangFeiJoined && !story.GuanYuJoined) prompt = "tavern";
            else if (Near(Smith, p)) prompt = ShopPanel;
            else if (Near(Innkeeper, p)) prompt = SavePanel;
            // Other residents need no button: they speak in their bubble (UpdateBubbles).
            if (prompt == Prompt) return;
            Prompt = prompt;
            Changed?.Invoke();
        }

        bool Near(Transform t, Vector3 p) => t && t.gameObject.activeInHierarchy && Vector3.Distance(Flat(t.position), Flat(p)) <= TalkRange;

        void UpdateBubbles()
        {
            if (Run == null || !Player) return;
            var p = Player.transform.position;
            bool changed = false;
            foreach (var r in Residents)
            {
                if (!r) continue;
                bool near = !Busy && r.gameObject.activeInHierarchy && Vector3.Distance(Flat(r.position), Flat(p)) <= BubbleRange;
                if (near == speaking.Contains(r)) continue;
                if (near) { speaking.Add(r); Face(r); }
                else speaking.Remove(r);
                changed = true;
            }
            if (changed) Changed?.Invoke();
        }
        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);

        // ---------- sequences ----------

        IEnumerator Say(IReadOnlyList<DialogueLine> lines)
        {
            Busy = true;
            SetControl(false);
            Prompt = null;
            try
            {
                foreach (var line in lines)
                {
                    Line = line; advance = false;
                    Changed?.Invoke();
                    yield return null;                  // the press that opened the dialogue must not skip line one
                    while (!advance) yield return null;
                }
            }
            finally
            {
                Line = null;
                Busy = false;
                SetControl(true);
                Changed?.Invoke();
            }
        }

        // At the notice board: Liu Bei reads and sighs, Zhang Fei strides in from off screen and scolds him, Liu Bei
        // answers one of two ways, and Zhang Fei joins either way, then proposes the tavern (user 2026-10-22).
        IEnumerator MeetZhangFei()
        {
            Busy = true;
            SetControl(false);
            Prompt = null;
            Changed?.Invoke();
            try
            {
                if (NoticeSpot) Place(Player.transform, NoticeSpot);
                if (NoticeBoard) StageMoves.Face(Player.transform, NoticeBoard.position);
                yield return Lines(NoticeRead);
                if (ZhangFei && ZhangFeiEntrance.Length > 0)
                {
                    StageMoves.Place(ZhangFei, ZhangFeiEntrance[0]);
                    ZhangFei.gameObject.SetActive(true);
                    yield return StageMoves.Walk(ZhangFei, ZhangFeiEntrance);
                    StageMoves.Face(ZhangFei, Player.transform.position);
                    StageMoves.Face(Player.transform, ZhangFei.position);
                }
                yield return Lines(ZhangFeiCallsOut);
                Line = new DialogueLine("liu_bei", "choice.prompt");
                Choices = ZhangFeiChoices;
                chosen = null;
                Changed?.Invoke();
                while (chosen == null) yield return null;
                int answer = chosen.Value;
                Choices = null;
                yield return Lines(ZhangFeiAnswers[answer]);
                // Joined only once the talk is over (GDD edge case); he falls in behind Liu Bei.
                Run.Story.Join(StoryState.ZhangFei);
                Vector3 from = ZhangFei ? ZhangFei.position - Player.transform.position : Vector3.back;
                if (ZhangFei) ZhangFei.gameObject.SetActive(false);
                ShowFollowers(from);
                ShowBlockers();
                yield return Lines(ZhangFeiJoins);
            }
            finally
            {
                Line = null;
                Choices = null;
                Busy = false;
                SetControl(true);
                Changed?.Invoke();
            }
        }

        // Lines inside a sequence that already holds the town.
        IEnumerator Lines(IReadOnlyList<DialogueLine> lines)
        {
            foreach (var line in lines)
            {
                Line = line; advance = false;
                Changed?.Invoke();
                yield return null;
                while (!advance) yield return null;
            }
            Line = null;
        }

        // Into the tavern with Zhang Fei: the indoor scene plays the meeting with Guan Yu (TavernStage).
        IEnumerator EnterTavern()
        {
            Busy = true;
            SetControl(false);
            Prompt = null;
            Changed?.Invoke();
            yield return FadeTo(1f);
            Run.NextArrival = GameRun.Arrival.FromTavernInside;
            SceneManager.LoadScene(GameRun.TavernScene);
        }

        // Simplified peach-garden oath (V1_SPEC §2.1): the game moves the three to the grove outside the west gate,
        // plays a few lines that cannot be skipped (GDD answer 9), and returns control inside the west gate (answer 7).
        IEnumerator PlayOath()
        {
            if (!Run.Story.BeginOath()) yield break;
            Busy = true;
            SetControl(false);
            Prompt = null;
            Changed?.Invoke();
            try
            {
                yield return FadeTo(1f);
                SetFollowers(false);
                Place(Player.transform, GroveSpots.Length > 0 ? GroveSpots[0] : GroveFocus);
                if (GuanYu && GroveSpots.Length > 1) { Place(GuanYu, GroveSpots[1]); GuanYu.gameObject.SetActive(true); }
                if (ZhangFei && GroveSpots.Length > 2) { Place(ZhangFei, GroveSpots[2]); ZhangFei.gameObject.SetActive(true); }
                if (Camera && GroveFocus) Camera.Target = GroveFocus;
                yield return FadeTo(0f);
                foreach (var line in OathScene)
                {
                    Line = line; advance = false;
                    Changed?.Invoke();
                    yield return null;
                    while (!advance) yield return null;
                }
                Line = null;
                yield return FadeTo(1f);
                if (GuanYu) GuanYu.gameObject.SetActive(false);
                if (ZhangFei) ZhangFei.gameObject.SetActive(false);
                Place(Player.transform, OathEndSpot);
                if (Camera) Camera.Target = Player.transform;
                ShowFollowers(Vector3.left);   // control resumes inside the west gate, the brothers behind
                Run.Story.CompleteOath();
                ShowBlockers();
                yield return FadeTo(0f);
            }
            finally
            {
                Line = null;
                Fade = 0f;
                if (Camera) Camera.Target = Player.transform;
                Busy = false;
                SetControl(true);
                Changed?.Invoke();
            }
        }

        // ---------- gates (user 2026-10-22) ----------

        // Guards turn Liu Bei back at a way out; the Yellow Turban leader at the south gate challenges him when he comes
        // near. True when either started.
        public bool CheckGates()
        {
            if (Run == null || Busy || Panel != null || !Player) return false;
            var p = Player.transform.position;
            if (TownInside.Contains(p)) wasInside = true;
            else if (!Gates.Any(g => Passage(g).Contains(p + Vector3.up))) wasInside = false;
            foreach (var gate in Gates)
                if (wasInside && gate.Guards && gate.Guards.gameObject.activeSelf && Passage(gate).Contains(p + Vector3.up))
                {
                    StartCoroutine(TurnBack(gate));
                    return true;
                }
            var leader = BlockerLeader;
            if (leader && Run.Story.CanLeaveTown(out _) && Vector3.Distance(Flat(leader.position), Flat(p)) <= ChallengeRange)
            {
                ChallengeAtGate();
                return Busy;
            }
            return false;
        }

        static Bounds Passage(CityGate g) => new Bounds(g.Center, g.Size);

        IEnumerator TurnBack(CityGate gate)
        {
            var guard = gate.Guards.childCount > 0 ? gate.Guards.GetChild(0) : null;
            Face(guard);
            // The west gate stays shut once the south one is open: point the way.
            bool southOpen = Gates.Any(g => g.Id == "south" && g.Guards && !g.Guards.gameObject.activeSelf);
            yield return Say(new[] { new DialogueLine("guard", southOpen ? "guard.stop.south" : "guard.stop") });
            var inward = gate.Inward.normalized;
            float depth = Mathf.Abs(Vector3.Dot(gate.Size, new Vector3(Mathf.Abs(inward.x), 0, Mathf.Abs(inward.z))));
            var back = new GameObject("turn back").transform;
            back.position = new Vector3(gate.Center.x, 0, gate.Center.z) + inward * (depth / 2f + 1.6f);
            Place(Player.transform, back);
            Destroy(back.gameObject);
            ShowFollowers(-inward);
            Changed?.Invoke();
        }

        // The leader at the gate calls out, the brothers answer, then the battle (user 2026-10-22).
        IEnumerator Confront()
        {
            string battle = Run.Story.NextBattleId;
            yield return Say(BattleScenes.BeforeBattle(battle));
            yield return LeaveTown();
        }

        // The group for the next battle stands at the gate once the brothers can leave town (after the oath).
        void ShowBlockers()
        {
            int next = Run.Story.CanLeaveTown(out _) ? Run.Story.BattlesDone : -1;
            // The south guards stand down once both brothers have joined (user 2026-10-22).
            bool bothJoined = Run.Story.GuanYuJoined && Run.Story.ZhangFeiJoined;
            foreach (var gate in Gates)
                if (gate.Guards && gate.Id == "south") gate.Guards.gameObject.SetActive(!bothJoined);
            for (int i = 0; i < GateBlockers.Length; i++)
                if (GateBlockers[i]) GateBlockers[i].gameObject.SetActive(i == next);
            if (GateWall) GateWall.enabled = next >= 0;
        }

        IEnumerator LeaveTown()
        {
            Busy = true;
            SetControl(false);
            pendingGateScene = GameRun.SceneFor(Run.Story.NextBattleId);
            Prompt = null;
            Changed?.Invoke();
            yield return FadeTo(1f);
            Run.NextArrival = GameRun.Arrival.ReturnFromBattle;
            SceneManager.LoadScene(pendingGateScene);
        }

        IEnumerator FadeTo(float target)
        {
            float start = Fade;
            for (float t = 0; t < FadeTime; t += Time.deltaTime)
            {
                Fade = Mathf.Lerp(start, target, t / FadeTime);
                Changed?.Invoke();
                yield return null;
            }
            Fade = target;
            Changed?.Invoke();
        }

        // Joined companions follow in join order (V1_SPEC §3), Guan Yu ahead of Zhang Fei when both are in.
        void ShowFollowers(Vector3 behind)
        {
            var trail = Player ? Player.GetComponent<PartyTrail>() : null;
            if (trail) trail.Reset(behind);
            int slot = 0;
            void Show(PartyFollower f, bool joined)
            {
                if (!f) return;
                f.gameObject.SetActive(joined);
                if (!joined) return;
                f.Slot = ++slot;
                f.Trail = trail;
                f.Snap();
            }
            Show(GuanYuFollower, Run.Story.GuanYuJoined);
            Show(ZhangFeiFollower, Run.Story.ZhangFeiJoined);
        }

        void SetFollowers(bool on)
        {
            if (GuanYuFollower) GuanYuFollower.gameObject.SetActive(on);
            if (ZhangFeiFollower) ZhangFeiFollower.gameObject.SetActive(on);
        }

        void SetControl(bool on) { if (Player) Player.InputEnabled = on; }

        static void Place(Transform who, Transform spot)
        {
            if (!who || !spot) return;
            var controller = who.GetComponent<CharacterController>();
            if (controller) controller.enabled = false;
            who.position = spot.position;
            if (controller) controller.enabled = true;
        }
    }
}
