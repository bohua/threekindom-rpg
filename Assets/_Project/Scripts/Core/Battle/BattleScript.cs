using System;
using System.Collections.Generic;
using System.Linq;

namespace ThreeKingdoms.Core.Battle
{
    // A named status with fixed stat effects (SYSTEMS_V02 §1.3). Only battle events apply statuses in 0.2.
    public sealed class StatusSpec
    {
        public string Id { get; }
        public string DisplayName { get; }
        public IReadOnlyList<(BattleStat stat, int percent)> Effects { get; }

        public StatusSpec(string id, string displayName, params (BattleStat stat, int percent)[] effects)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("状态缺少 Id。");
            Id = id; DisplayName = displayName; Effects = effects;
        }

        // 混乱: physical and strategy attack −30%, physical defence −20%; no random actions (V1 has no randomness).
        public static readonly StatusSpec Chaos = new StatusSpec("chaos", "混乱",
            (BattleStat.PhysicalAttack, -30), (BattleStat.StrategyAttack, -30), (BattleStat.PhysicalDefense, -20));

        static readonly Dictionary<string, StatusSpec> all = new Dictionary<string, StatusSpec> { [Chaos.Id] = Chaos };
        public static StatusSpec Find(string id) => id != null && all.TryGetValue(id, out var s) ? s : null;
    }

    public enum TriggerKind { TurnStart, UnitHpBelow, UnitRetreated }

    public enum ScriptActionKind { Banner, Dialogue, ApplyStatus, DamageRatio, Fx, Retreat, EndBattle, SetObjective }

    // When a battle event fires (SYSTEMS_V02 §1.2). Units are named by general id; groups by "enemy_all",
    // "player_all" or "ally_all".
    public sealed class ScriptTrigger
    {
        public TriggerKind Kind { get; }
        public int Round { get; }                // TurnStart
        public string Unit { get; }              // UnitHpBelow, UnitRetreated
        public int Percent { get; }              // UnitHpBelow: fires when troops cross below this share of max
        public RetreatReason? Reason { get; }    // UnitRetreated: optional filter

        ScriptTrigger(TriggerKind kind, int round, string unit, int percent, RetreatReason? reason)
        { Kind = kind; Round = round; Unit = unit; Percent = percent; Reason = reason; }

        public static ScriptTrigger TurnStart(int round) => new ScriptTrigger(TriggerKind.TurnStart, round, null, 0, null);
        public static ScriptTrigger HpBelow(string unit, int percent) => new ScriptTrigger(TriggerKind.UnitHpBelow, 0, unit, percent, null);
        public static ScriptTrigger Retreated(string unit, RetreatReason? reason = null) => new ScriptTrigger(TriggerKind.UnitRetreated, 0, unit, 0, reason);
    }

    public sealed class ScriptAction
    {
        public ScriptActionKind Kind { get; }
        public string Text { get; }       // banner / objective text, dialogue id, fx id, status id
        public string Target { get; }     // unit id or group
        public int Value { get; }         // status turns, damage percent
        public BattleOutcome Result { get; }

        ScriptAction(ScriptActionKind kind, string text = null, string target = null, int value = 0, BattleOutcome result = BattleOutcome.Ongoing)
        { Kind = kind; Text = text; Target = target; Value = value; Result = result; }

        public static ScriptAction Banner(string text) => new ScriptAction(ScriptActionKind.Banner, text);
        public static ScriptAction Dialogue(string id) => new ScriptAction(ScriptActionKind.Dialogue, id);
        public static ScriptAction ApplyStatus(string target, string status, int turns) => new ScriptAction(ScriptActionKind.ApplyStatus, status, target, turns);
        public static ScriptAction DamageRatio(string target, int percent) => new ScriptAction(ScriptActionKind.DamageRatio, null, target, percent);
        public static ScriptAction Fx(string id) => new ScriptAction(ScriptActionKind.Fx, id);
        public static ScriptAction Retreat(string target) => new ScriptAction(ScriptActionKind.Retreat, null, target);
        public static ScriptAction EndBattle(BattleOutcome result) => new ScriptAction(ScriptActionKind.EndBattle, result: result);
        public static ScriptAction Objective(string text) => new ScriptAction(ScriptActionKind.SetObjective, text);
    }

    public sealed class ScriptEvent
    {
        public string Id { get; }
        public ScriptTrigger When { get; }
        public bool Once { get; }
        public IReadOnlyList<ScriptAction> Actions { get; }

        public ScriptEvent(string id, ScriptTrigger when, IEnumerable<ScriptAction> actions, bool once = true)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("战场事件缺少 Id。");
            Id = id; When = when ?? throw new ArgumentNullException(nameof(when)); Once = once;
            Actions = (actions ?? Enumerable.Empty<ScriptAction>()).ToArray();
        }
    }

    // The data-driven battle events of one encounter (SYSTEMS_V02 §1). Validated when built: unit ids exist,
    // no two events start on the same round with conflicting results, nothing follows end_battle.
    public sealed class BattleScript
    {
        public const string EnemyAll = "enemy_all", PlayerAll = "player_all", AllyAll = "ally_all";
        public const int MaxDepth = 3;

        public IReadOnlyList<ScriptEvent> Events { get; }

        public BattleScript(IEnumerable<ScriptEvent> events) { Events = (events ?? Enumerable.Empty<ScriptEvent>()).ToArray(); }

        public static readonly BattleScript Empty = new BattleScript(null);

        public void Validate(IEnumerable<string> unitIds, string owner)
        {
            var ids = new HashSet<string>(unitIds);
            bool Known(string t) => t == EnemyAll || t == PlayerAll || t == AllyAll || ids.Contains(t);
            var dup = Events.GroupBy(e => e.Id).FirstOrDefault(g => g.Count() > 1);
            if (dup != null) throw new ArgumentException($"{owner}: 战场事件 Id「{dup.Key}」重复。");
            foreach (var e in Events)
            {
                if (e.When.Kind != TriggerKind.TurnStart && !Known(e.When.Unit)) throw new ArgumentException($"{owner}/{e.Id}: 触发条件的单位「{e.When.Unit}」不存在。");
                if (e.When.Kind == TriggerKind.TurnStart && e.When.Round < 1) throw new ArgumentException($"{owner}/{e.Id}: 回合数必须 ≥ 1。");
                for (int i = 0; i < e.Actions.Count; i++)
                {
                    var a = e.Actions[i];
                    if ((a.Kind == ScriptActionKind.ApplyStatus || a.Kind == ScriptActionKind.DamageRatio || a.Kind == ScriptActionKind.Retreat) && !Known(a.Target))
                        throw new ArgumentException($"{owner}/{e.Id}: 动作目标「{a.Target}」不存在。");
                    if (a.Kind == ScriptActionKind.ApplyStatus && StatusSpec.Find(a.Text) == null) throw new ArgumentException($"{owner}/{e.Id}: 未知状态「{a.Text}」。");
                    if (a.Kind == ScriptActionKind.EndBattle && i != e.Actions.Count - 1) throw new ArgumentException($"{owner}/{e.Id}: end_battle 之后不能再有动作。");
                }
            }
            var clash = Events.Where(e => e.When.Kind == TriggerKind.TurnStart && e.Actions.Any(a => a.Kind == ScriptActionKind.EndBattle))
                .GroupBy(e => e.When.Round).FirstOrDefault(g => g.Select(e => e.Actions.Last().Result).Distinct().Count() > 1);
            if (clash != null) throw new ArgumentException($"{owner}: 第 {clash.Key} 回合的事件结果互相冲突。");
        }
    }

    // A friendly unit that takes no orders (SYSTEMS_V02 §2.2), e.g. Dong Zhuo's army.
    public readonly struct AllySlot
    {
        public readonly GeneralSpec General;
        public readonly int Level;
        public readonly bool Escort;
        public AllySlot(GeneralSpec general, int level, bool escort) { General = general ?? throw new ArgumentNullException(nameof(general)); Level = level; Escort = escort; }
    }
}
