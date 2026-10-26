using System;
using System.Linq;
using ThreeKingdoms.Core.Battle;
using UnityEngine;

namespace ThreeKingdoms.Data.Battle
{
    [Serializable]
    public struct EnemyEntry
    {
        public GeneralDefinition General;
        [Min(1)] public int Level;
        [Tooltip("优先攻击护卫目标（SYSTEMS_V02 §2.2 focus_escort）")] public bool FocusEscort;
    }

    // A friendly unit that takes no orders (0.2, SYSTEMS_V02 §2.2).
    [Serializable]
    public struct AllyEntry
    {
        public GeneralDefinition General;
        [Min(1)] public int Level;
        [Tooltip("护卫目标：撤退即失败")] public bool Escort;
    }

    [Serializable]
    public struct BattleActionEntry
    {
        public ScriptActionKind Kind;
        [Tooltip("横幅／目标栏文字、对白 id、特效 id 或状态 id")] public string Text;
        [Tooltip("单位 id，或 enemy_all／player_all／ally_all")] public string Target;
        [Tooltip("状态回合数，或按最大兵力扣除的百分比")] public int Value;
        public BattleOutcome Result;

        public ScriptAction ToRuntime() => Kind switch
        {
            ScriptActionKind.Banner => ScriptAction.Banner(Text),
            ScriptActionKind.Dialogue => ScriptAction.Dialogue(Text),
            ScriptActionKind.ApplyStatus => ScriptAction.ApplyStatus(Target, Text, Value),
            ScriptActionKind.DamageRatio => ScriptAction.DamageRatio(Target, Value),
            ScriptActionKind.Fx => ScriptAction.Fx(Text),
            ScriptActionKind.Retreat => ScriptAction.Retreat(Target),
            ScriptActionKind.EndBattle => ScriptAction.EndBattle(Result),
            _ => ScriptAction.Objective(Text),
        };
    }

    // One battle event (SYSTEMS_V02 §1.2): when, and the actions in order.
    [Serializable]
    public struct BattleEventEntry
    {
        public string Id;
        public TriggerKind When;
        [Tooltip("turn_start 的回合数")] public int Round;
        [Tooltip("unit_hp_below／unit_retreated 的单位 id 或分组")] public string Unit;
        [Tooltip("unit_hp_below：跨过此百分比时触发")] public int Percent;
        public bool FilterReason;
        public RetreatReason Reason;
        [Tooltip("默认只触发一次")] public bool Repeat;
        public BattleActionEntry[] Actions;

        public ScriptEvent ToRuntime()
        {
            var trigger = When switch
            {
                TriggerKind.UnitHpBelow => ScriptTrigger.HpBelow(Unit, Percent),
                TriggerKind.UnitRetreated => ScriptTrigger.Retreated(Unit, FilterReason ? Reason : (RetreatReason?)null),
                _ => ScriptTrigger.TurnStart(Round),
            };
            return new ScriptEvent(Id, trigger, (Actions ?? Array.Empty<BattleActionEntry>()).Select(a => a.ToRuntime()), !Repeat);
        }
    }

    [CreateAssetMenu(menuName = "ThreeKingdoms/Battle/Encounter")]
    public sealed class EncounterDefinition : ScriptableObject
    {
        [Tooltip("与剧情标志一致：battle_1…battle_6")]
        public string Id;
        public string DisplayName;
        public EnemyEntry[] Enemies = Array.Empty<EnemyEntry>();
        public EnemyAiKind Ai;
        [Min(1)] public int SkillEveryRounds = 3;
        [Min(0)] public int ExpReward;
        [Min(0)] public int CopperReward;
        [Header("0.2 (SYSTEMS_V02)")]
        public AllyEntry[] Allies = Array.Empty<AllyEntry>();
        public BattleEventEntry[] Events = Array.Empty<BattleEventEntry>();

        public BattleScript Script() => new BattleScript((Events ?? Array.Empty<BattleEventEntry>()).Select(e => e.ToRuntime()));
    }
}
