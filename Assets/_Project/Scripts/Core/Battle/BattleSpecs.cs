using System;
using System.Collections.Generic;
using System.Linq;

namespace ThreeKingdoms.Core.Battle
{
    // Immutable rule data (ADR-0002 §2). Built from ScriptableObjects by ThreeKingdoms.Data, or directly in tests.

    public sealed class TroopTypeSpec
    {
        public TroopType Type { get; }
        public int TroopBonus { get; }
        public int DefenseBonus { get; }
        public int BaseSpeed { get; }
        public bool PlayerUsable { get; }

        public TroopTypeSpec(TroopType type, int troopBonus, int defenseBonus, int baseSpeed, bool playerUsable)
        {
            Type = type; TroopBonus = troopBonus; DefenseBonus = defenseBonus; BaseSpeed = baseSpeed; PlayerUsable = playerUsable;
        }
    }

    public sealed class TroopTable
    {
        readonly Dictionary<TroopType, TroopTypeSpec> types;
        readonly HashSet<(TroopType, TroopType)> advantages;

        public TroopTable(IEnumerable<TroopTypeSpec> entries, IEnumerable<(TroopType attacker, TroopType defender)> advantagePairs)
        {
            types = new Dictionary<TroopType, TroopTypeSpec>();
            foreach (var e in entries)
            {
                if (types.ContainsKey(e.Type)) throw new ArgumentException($"兵种 {e.Type} 重复定义。");
                types.Add(e.Type, e);
            }
            advantages = new HashSet<(TroopType, TroopType)>(advantagePairs);
        }

        public TroopTypeSpec Get(TroopType type) =>
            types.TryGetValue(type, out var spec) ? spec : throw new KeyNotFoundException($"兵种表缺少 {type}。");

        public bool HasAdvantage(TroopType attacker, TroopType defender) => advantages.Contains((attacker, defender));
    }

    public sealed class BattleConfig
    {
        public int AdvantagePercent { get; }     // 140: ×1.4 once, at the final step
        public int GuardPercent { get; }         // 50: defending halves damage
        public int MinModifierPercent { get; }   // 20: stat multiplier floor 0.2
        public int MaxAttribute { get; }         // 100
        public IReadOnlyList<int> ExpTable { get; } // cumulative exp for level 1..N; N is the level cap

        public int MaxLevel => ExpTable.Count;

        public BattleConfig(int advantagePercent, int guardPercent, int minModifierPercent, int maxAttribute, IEnumerable<int> expTable)
        {
            AdvantagePercent = advantagePercent; GuardPercent = guardPercent; MinModifierPercent = minModifierPercent;
            MaxAttribute = maxAttribute;
            ExpTable = expTable.ToArray();
            if (ExpTable.Count == 0 || ExpTable[0] != 0) throw new ArgumentException("经验表必须从 0 开始。");
            for (int i = 1; i < ExpTable.Count; i++)
                if (ExpTable[i] <= ExpTable[i - 1]) throw new ArgumentException("经验表必须递增。");
        }
    }

    public sealed class SkillSpec
    {
        public string Id { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public int TechCost { get; }
        public SkillTarget Target { get; }
        public DamageKind Damage { get; }
        public int PowerPercent { get; }          // 150 = ×1.5; 0 = no damage
        public int HealPercent { get; }           // share of the target's max troops; 0 = no heal
        public BattleStat ModifierStat { get; }
        public int ModifierPercent { get; }       // -20 = −20%; 0 = no modifier
        public int ModifierTurns { get; }         // includes the casting round

        public bool TargetsAllies => Target == SkillTarget.SingleAlly;
        public bool IsSingleTarget => Target != SkillTarget.AllEnemies;

        public SkillSpec(string id, string displayName, string description, int techCost, SkillTarget target, DamageKind damage,
            int powerPercent, int healPercent = 0, BattleStat modifierStat = BattleStat.PhysicalAttack, int modifierPercent = 0, int modifierTurns = 0)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("技能缺少 Id。");
            Id = id; DisplayName = displayName; Description = description; TechCost = techCost; Target = target; Damage = damage;
            PowerPercent = powerPercent; HealPercent = healPercent;
            ModifierStat = modifierStat; ModifierPercent = modifierPercent; ModifierTurns = modifierTurns;
        }

        public override string ToString() => Id;
    }

    public readonly struct SkillUnlock
    {
        public readonly int Level;
        public readonly SkillSpec Skill;
        public SkillUnlock(int level, SkillSpec skill) { Level = level; Skill = skill ?? throw new ArgumentNullException(nameof(skill)); }
    }

    // One general leads one unit; troops are its HP. Shared by both sides.
    public sealed class GeneralSpec
    {
        public string Id { get; }
        public string DisplayName { get; }
        public TroopType Troop { get; }
        public int Force { get; }
        public int Command { get; }
        public int Intellect { get; }
        public int ForceGrowth { get; }
        public int CommandGrowth { get; }
        public int IntellectGrowth { get; }
        public IReadOnlyList<SkillUnlock> Skills { get; }

        public GeneralSpec(string id, string displayName, TroopType troop, int force, int command, int intellect,
            int forceGrowth = 0, int commandGrowth = 0, int intellectGrowth = 0, IEnumerable<SkillUnlock> skills = null)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("武将缺少 Id。");
            Id = id; DisplayName = displayName; Troop = troop;
            Force = force; Command = command; Intellect = intellect;
            ForceGrowth = forceGrowth; CommandGrowth = commandGrowth; IntellectGrowth = intellectGrowth;
            Skills = (skills ?? Enumerable.Empty<SkillUnlock>()).OrderBy(s => s.Level).ToArray();
        }

        // Fixed growth, auto-learned skills: every skill unlocked at or below the level, in unlock order.
        public IEnumerable<SkillSpec> SkillsAt(int level) => Skills.Where(s => s.Level <= level).Select(s => s.Skill);

        public override string ToString() => Id;
    }

    public readonly struct EnemySlot
    {
        public readonly GeneralSpec General;
        public readonly int Level;
        // Goes for the escort target first (SYSTEMS_V02 §2.2 focus_escort).
        public readonly bool FocusEscort;
        public EnemySlot(GeneralSpec general, int level, bool focusEscort = false)
        { General = general ?? throw new ArgumentNullException(nameof(general)); Level = level; FocusEscort = focusEscort; }
    }

    public sealed class EncounterSpec
    {
        public string Id { get; }
        public string DisplayName { get; }
        public IReadOnlyList<EnemySlot> Enemies { get; }
        public EnemyAiKind Ai { get; }
        public int SkillEveryRounds { get; }     // FacingSlot: skills on rounds that are multiples of this
        public int ExpReward { get; }
        public int CopperReward { get; }
        // 0.2 (SYSTEMS_V02): friendly units that take no orders, and the battle's scripted events.
        public IReadOnlyList<AllySlot> Allies { get; }
        public BattleScript Script { get; }

        public EncounterSpec(string id, string displayName, IEnumerable<EnemySlot> enemies, EnemyAiKind ai, int skillEveryRounds, int expReward, int copperReward,
            IEnumerable<AllySlot> allies = null, BattleScript script = null)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("遭遇缺少 Id。");
            Id = id; DisplayName = displayName; Enemies = enemies.ToArray(); Ai = ai;
            SkillEveryRounds = Math.Max(1, skillEveryRounds); ExpReward = expReward; CopperReward = copperReward;
            Allies = (allies ?? Enumerable.Empty<AllySlot>()).ToArray();
            Script = script ?? BattleScript.Empty;
            if (Enemies.Count == 0) throw new ArgumentException($"遭遇 {id} 没有敌人。");
            if (Script.Events.Count > 0)
                Script.Validate(Enemies.Select(e => e.General.Id).Concat(Allies.Select(a => a.General.Id)), id);
        }
    }

    // A battle-usable item. V1 has exactly one: army rations (军粮). Values owned by the equipment & items system.
    public sealed class ItemSpec
    {
        public string Id { get; }
        public string DisplayName { get; }
        public int HealPercent { get; }

        public ItemSpec(string id, string displayName, int healPercent)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("道具缺少 Id。");
            Id = id; DisplayName = displayName; HealPercent = healPercent;
        }
    }

    // Everything the battle needs about one of our generals, assembled by Gameplay from the progression
    // system (level, learned skills) and the equipment system (bonus). The battle never reads those systems.
    public sealed class CombatantInput
    {
        public GeneralSpec General { get; }
        public int Level { get; }
        public IReadOnlyList<SkillSpec> Skills { get; }
        public StatBonus Equipment { get; }

        public CombatantInput(GeneralSpec general, int level, IEnumerable<SkillSpec> skills, StatBonus equipment = default)
        {
            General = general ?? throw new ArgumentNullException(nameof(general));
            Level = level;
            Skills = (skills ?? Enumerable.Empty<SkillSpec>()).ToArray();
            Equipment = equipment;
        }

        // Fixed growth, auto-learned skills (character-progression GDD).
        public static CombatantInput AtLevel(GeneralSpec general, int level, StatBonus equipment = default) =>
            new CombatantInput(general, level, general.SkillsAt(level), equipment);
    }
}
