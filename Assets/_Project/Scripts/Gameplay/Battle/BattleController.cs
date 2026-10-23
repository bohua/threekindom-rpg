using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ThreeKingdoms.Core.Battle;
using ThreeKingdoms.Data.Battle;
using UnityEngine;
using UnityEngine.InputSystem;
using ThreeKingdoms.Gameplay.Story;

namespace ThreeKingdoms.Gameplay.Battle
{
    // The only component that drives the battle core (ADR-0002 §8):
    // Commanding → Resolving (core) → Playback (events) → next round, or Finished.
    // The UI reads state and events from here and submits commands through here.
    public sealed class BattleController : MonoBehaviour, IPanelHost
    {
        public enum Phase { Preparing, Starting, Commanding, Playback, Finished }

        public BattleCatalog Catalog;
        public string EncounterId = "battle_1";
        [Tooltip("Our units by line-up slot (Liu Bei, Guan Yu, Zhang Fei)")] public BattleUnitView[] PlayerViews = Array.Empty<BattleUnitView>();
        [Tooltip("Enemy units by slot; extra views are hidden")] public BattleUnitView[] EnemyViews = Array.Empty<BattleUnitView>();
        [Tooltip("Stand-in party when this scene is opened directly: battle 1 = 0 exp / 120 copper, battle 2 = 10 / 150, battle 3 = 30 / 200 (V1_SPEC §7–8)")]
        public int StartRations = 2, StartCopper = 120, StartExp;
        [Tooltip("Development run only: scene the next-battle button loads after a victory; empty after the last battle")] public string NextScene;
        [Tooltip("Start / menu key: during preparation, asks to end it and sortie (user 2026-10-22)")] public InputActionReference QuitAction;
        [Range(0.5f, 4f)] public float PlaybackSpeed = 1f;
        public bool Fast;
        [Tooltip("Auto battle (user 2026-10-22): the party's commands are chosen by PartyAutoPilot")] public bool Auto;
        // Raised when Start is pressed while preparing; the HUD asks before sortieing.
        public event Action SortieRequested;

        public BattleData Data { get; private set; }
        public DevPartyProgress Progress { get; private set; }
        public BattleSession Session { get; private set; }
        public Phase Current { get; private set; } = Phase.Starting;
        public BattleUnit Actor => Current == Phase.Commanding && actorIndex < pending.Count ? pending[actorIndex] : null;
        public IReadOnlyDictionary<BattleUnit, BattleCommand> Commands => commands;
        public DevPartyProgress.Applied LastApplied { get; private set; }

        public event Action Changed;                       // phase, actor or commands changed
        public event Action<BattleEvent> EventPlayed;      // one resolution event is being shown
        public event Action BattleStarted;
        public event Action PreparationStarted;
        public event Action<BattleUnit, BattleUnit> TargetPreviewed;   // actor, target (teaching hints)
        public event Action<BattleCommand> CommandChosen;
        // Shop-style panels opened from preparation ("outfit"), same contract as the town's (ShopHud listens to both).
        public event Action<string> PanelOpened, PanelClosed;

        readonly Dictionary<BattleUnit, BattleCommand> commands = new Dictionary<BattleUnit, BattleCommand>();
        readonly Dictionary<BattleUnit, BattleUnitView> views = new Dictionary<BattleUnit, BattleUnitView>();
        readonly List<BattleUnit> pending = new List<BattleUnit>();
        int actorIndex;

        float Speed => PlaybackSpeed * (Fast ? 3f : 1f);
        public float PlaybackRate => Speed;

        void OnEnable() { if (QuitAction) QuitAction.action.Enable(); }
        void OnDisable() { if (QuitAction) QuitAction.action.Disable(); }

        void Update()
        {
            if (QuitAction && QuitAction.action.WasPressedThisFrame() && Current == Phase.Preparing && Panel == null && CanConfirm)
                SortieRequested?.Invoke();
            if (Auto && Current == Phase.Commanding && Actor != null) Choose(PartyAutoPilot.Choose(Session.State, Actor));
        }

        BattleUnit previewTarget;

        // UI calls this while a target button is focused; null clears the preview.
        public void PreviewTarget(BattleUnit unit)
        {
            if (previewTarget == unit) return;
            ViewOf(previewTarget)?.SetTargeted(false);
            previewTarget = Current == Phase.Commanding ? unit : null;
            ViewOf(previewTarget)?.SetTargeted(true);
            if (previewTarget != null) TargetPreviewed?.Invoke(Actor, previewTarget);
        }

        void Start()
        {
            Data = Catalog.ToRuntime();
            // Part of a play-through (ADR-0006) — or, opened on its own, a development run at this battle's expected state.
            Run = GameRun.Current ?? GameRun.ForBattle(Catalog, GameRun.BattleIndex(EncounterId), StartExp, StartCopper, StartRations);
            Data = Run.Data;
            Progress = Run.Progress;
            slotHomes = PlayerViews.Select(v => v ? v.Home : Vector3.zero).ToArray();
            if (NeedsPreparation) BeginPreparation();
            else StartBattle();
        }

        // ---------- pre-battle preparation (game-ui GDD rules 16–17) ----------

        Vector3[] slotHomes = Array.Empty<Vector3>();
        readonly List<GeneralSpec> selected = new List<GeneralSpec>();

        // A play-through prepares every battle; a battle scene opened on its own (development) starts at once.
        public bool NeedsPreparation => Run != null && !Run.IsDevelopment;
        public string EncounterName => Data.Encounter(EncounterId).DisplayName;
        public IReadOnlyList<GeneralSpec> Selected => selected;
        public IEnumerable<GeneralSpec> Available => Progress.Party.Where(g => Run.Story.HasJoined(g.Id));
        public bool CanConfirm => Current == Phase.Preparing && selected.Count > 0;
        public string Panel { get; private set; }

        // Enemy intel shown before the battle (answer 4): name, troop type, level, troops; no skills.
        public IEnumerable<(GeneralSpec general, int level, int troops)> EnemyIntel =>
            Data.Encounter(EncounterId).Enemies.Select(e => (e.General, e.Level,
                BattleRules.Compute(e.General, e.Level, default, Data.Troops, Data.Config).MaxTroops));

        public void BeginPreparation()
        {
            StopAllCoroutines();
            Current = Phase.Preparing;
            if (selected.Count == 0 || selected.Any(g => !Run.Story.HasJoined(g.Id)))
            {
                selected.Clear();
                selected.AddRange(Available.Take(3));
            }
            foreach (var v in PlayerViews) if (v) v.SetSelected(false);
            PreparationStarted?.Invoke();
            Changed?.Invoke();
        }

        // Toggles a general in or out; newly added generals take the next slot (1–3 generals, no empty line-up).
        public void ToggleSelected(GeneralSpec general)
        {
            if (Current != Phase.Preparing || !Run.Story.HasJoined(general.Id)) return;
            if (!selected.Remove(general) && selected.Count < 3) selected.Add(general);
            Changed?.Invoke();
        }

        // Confirming builds the snapshot the battle starts from and writes the rolling pre-battle save (rule 9).
        public void ConfirmPreparation()
        {
            if (!CanConfirm) return;
            AutoSave();
            StartBattle();
        }

        public void OpenPanel(string panel)
        {
            if (Current != Phase.Preparing || Panel != null) return;
            Panel = panel;
            PanelOpened?.Invoke(panel);
        }

        public void ClosePanel()
        {
            if (Panel == null) return;
            string closed = Panel;
            Panel = null;
            PanelClosed?.Invoke(closed);
            Changed?.Invoke();   // gear may have changed: refresh the preparation summary
        }

        public bool AutoSaved { get; private set; }

        // The rolling pre-battle save (save-system rule 2): the state the battle starts from, which is also what a
        // retry restores (nothing is applied on defeat). Development runs never touch the player's saves.
        void AutoSave()
        {
            if (Run.IsDevelopment || Run.Story.NextBattleId != EncounterId) return;
            try { Run.SaveTo(ThreeKingdoms.Save.SaveSlots.Auto, EncounterId); AutoSaved = true; }
            catch (Exception e) when (e is System.IO.IOException || e is UnauthorizedAccessException) { Debug.LogWarning("Autosave failed: " + e.Message); }
        }

        public BattleUnitView ViewOf(BattleUnit unit) => unit != null && views.TryGetValue(unit, out var v) ? v : null;

        // Also used for "retry" (nothing was applied on defeat) and "fight again" (levels carry over, no second reward).
        public void StartBattle()
        {
            StopAllCoroutines();
            Current = Phase.Starting;
            var lineup = NeedsPreparation && selected.Count > 0 ? Progress.Lineup(selected) : Progress.Lineup(Run.Story.HasJoined);
            Session = new BattleSession(Data.Encounter(EncounterId), lineup, Data.Ration, Progress.Rations, Data.Config, Data.Troops);
            views.Clear();
            BindPlayers(Session.State.Players);
            Bind(Session.State.Enemies, EnemyViews);
            LastApplied = null;
            BattleStarted?.Invoke();
            BeginCommanding();
        }

        // Our views belong to generals (party order: Liu Bei, Guan Yu, Zhang Fei); each stands at its line-up slot.
        void BindPlayers(IReadOnlyList<BattleUnit> units)
        {
            var used = new HashSet<BattleUnitView>();
            foreach (var u in units)
            {
                int index = Progress.Party.ToList().FindIndex(g => g.Id == u.Id);
                var view = index >= 0 && index < PlayerViews.Length ? PlayerViews[index] : null;
                if (!view) continue;
                if (u.Slot < slotHomes.Length) view.SetHome(slotHomes[u.Slot]);
                view.ResetView();
                views[u] = view;
                used.Add(view);
            }
            foreach (var v in PlayerViews) if (v && !used.Contains(v)) v.gameObject.SetActive(false);
        }

        void Bind(IReadOnlyList<BattleUnit> units, BattleUnitView[] slots)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (!slots[i]) continue;
                slots[i].ResetView();
                if (i < units.Count) views[units[i]] = slots[i];
                else slots[i].gameObject.SetActive(false);
            }
        }

        void BeginCommanding()
        {
            commands.Clear();
            pending.Clear();
            pending.AddRange(Session.State.Players.Where(u => u.Active));
            actorIndex = 0;
            Current = Phase.Commanding;
            HighlightActor();
            Changed?.Invoke();
        }

        void HighlightActor()
        {
            PreviewTarget(null);
            foreach (var pair in views) pair.Value.SetSelected(pair.Key == Actor);
        }

        // Before the whole party is submitted any command can be changed (step back with Undo).
        public void Choose(BattleCommand command)
        {
            if (Current != Phase.Commanding || command == null || command.Actor != Actor) return;
            commands[command.Actor] = command;
            CommandChosen?.Invoke(command);
            actorIndex++;
            if (actorIndex >= pending.Count) { StartCoroutine(Resolve()); return; }
            HighlightActor();
            Changed?.Invoke();
        }

        public void Undo()
        {
            if (Current != Phase.Commanding || actorIndex == 0) return;
            actorIndex--;
            commands.Remove(pending[actorIndex]);
            HighlightActor();
            Changed?.Invoke();
        }

        IEnumerator Resolve()
        {
            Current = Phase.Playback;
            HighlightActor();
            Changed?.Invoke();
            var mine = pending.Select(u => commands[u]).ToList();
            var log = Session.PlayRound(mine);
            yield return Playback(log);
            foreach (var v in views.Values) v.SetDefending(false);
            if (Session.Result != null) Finish();
            else BeginCommanding();
        }

        IEnumerator Playback(IReadOnlyList<BattleEvent> log)
        {
            for (int i = 0; i < log.Count; i++)
            {
                var e = log[i];
                EventPlayed?.Invoke(e);
                var actor = ViewOf(e.Actor);
                var target = ViewOf(e.Target);
                switch (e.Kind)
                {
                    case BattleEventKind.Defend:
                        actor?.SetDefending(true);
                        yield return Wait(0.35f);
                        break;
                    case BattleEventKind.ActionStarted:
                        if (!actor) break;
                        bool melee = e.Command == CommandKind.Attack ||
                            (e.Command == CommandKind.Skill && e.Skill != null && e.Skill.Damage != DamageKind.None && e.Skill.IsSingleTarget);
                        if (melee && target && target.gameObject.activeSelf) yield return actor.Lunge(target.Home, Speed);
                        else yield return actor.Cast(Speed);
                        break;
                    case BattleEventKind.Damage:
                        if (target) yield return target.Hit(Speed);
                        yield return Wait(0.15f);
                        break;
                    case BattleEventKind.Retreated:
                        if (target) yield return target.Retreat(Speed);
                        break;
                    case BattleEventKind.RoundEnded:
                    case BattleEventKind.ModifierExpired:
                        break;
                    default:
                        yield return Wait(0.45f);
                        break;
                }
            }
            yield return Wait(0.2f);
        }

        public GameRun Run { get; private set; }
        bool Won => Session?.Result?.Outcome == BattleOutcome.Victory;
        // After a victory: back to town (play-through), the next battle (development run), or the ending (last battle).
        public bool HasNextBattle => Won && Run.IsDevelopment && !string.IsNullOrEmpty(NextScene);
        public bool CanReturnToTown => Won && !Run.IsDevelopment && Run.Story.NextBattleId != null;
        public bool ReachedEnding => Won && Run.Story.NextBattleId == null;

        public void GoToNextBattle()
        {
            if (!HasNextBattle) return;
            UnityEngine.SceneManagement.SceneManager.LoadScene(NextScene);
        }

        public void ReturnToTown()
        {
            if (!CanReturnToTown) return;
            Run.NextArrival = GameRun.Arrival.ReturnFromBattle;
            UnityEngine.SceneManagement.SceneManager.LoadScene(GameRun.TownScene);
        }

        // Alpha 0.1 ending (story-events rule 19): narration, then "test version ends"; restart starts a new game.
        public void BeginEnding() { if (ReachedEnding) Run.Story.BeginEnding(); }

        public void FinishEnding()
        {
            if (Run.Story.Ending == ThreeKingdoms.Core.Story.SequenceState.Playing) Run.Story.CompleteEnding();
        }

        // After the test-version end or a defeat (save-system answer 5): back to the title, where any save can be loaded.
        public void ReturnToTitle()
        {
            GameRun.Clear();
            UnityEngine.SceneManagement.SceneManager.LoadScene(GameRun.TitleScene);
        }

        IEnumerator Wait(float seconds) { yield return new WaitForSeconds(seconds / Speed); }

        void Finish()
        {
            Current = Phase.Finished;
            // The story is the single reward-once gate: only the first win of the next battle grants exp and copper.
            bool firstClear = Session.Result.Outcome == BattleOutcome.Victory && Run.Story.RecordVictory(Session.Encounter.Id);
            LastApplied = Progress.Apply(Session.Result, firstClear);
            HighlightActor();
            Changed?.Invoke();
        }
    }
}
