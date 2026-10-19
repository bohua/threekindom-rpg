using System;
using System.Collections.Generic;
using System.Linq;
using ThreeKingdoms.Core.Battle;
using UnityEngine;

namespace ThreeKingdoms.Data.Battle
{
    // Entry point for all battle data. ToRuntime() converts every asset once into immutable Core specs and
    // validates references (ADR-0002 §2; ToRuntime naming from ADR-0001).
    [CreateAssetMenu(menuName = "ThreeKingdoms/Battle/Battle Catalog")]
    public sealed class BattleCatalog : ScriptableObject
    {
        public BattleConfigDefinition Config;
        public TroopTableDefinition Troops;
        public ItemDefinition Ration;
        [Tooltip("我方可操作武将（首版：刘备、关羽、张飞）")]
        public GeneralDefinition[] Party = Array.Empty<GeneralDefinition>();
        public EncounterDefinition[] Encounters = Array.Empty<EncounterDefinition>();

        public BattleData ToRuntime()
        {
            if (!Config || !Troops || !Ration) throw new InvalidOperationException($"{name}: 缺少战斗配置、兵种表或军粮。");
            var config = new BattleConfig(Config.AdvantagePercent, Config.GuardPercent, Config.MinModifierPercent, Config.MaxAttribute, Config.ExpTable);
            var troops = new TroopTable(
                Troops.Types.Select(t => new TroopTypeSpec(t.Type, t.TroopBonus, t.DefenseBonus, t.BaseSpeed, t.PlayerUsable)),
                Troops.Advantages.Select(a => (a.Attacker, a.Defender)));
            foreach (TroopType t in Enum.GetValues(typeof(TroopType))) troops.Get(t);   // every type must have numbers

            var skills = new Dictionary<BattleSkillDefinition, SkillSpec>();
            var generals = new Dictionary<GeneralDefinition, GeneralSpec>();

            SkillSpec Skill(BattleSkillDefinition def, string owner)
            {
                if (!def) throw new InvalidOperationException($"{owner}: 技能引用为空。");
                if (!skills.TryGetValue(def, out var spec)) skills[def] = spec = def.ToRuntime();
                return spec;
            }

            GeneralSpec General(GeneralDefinition def, string owner)
            {
                if (!def) throw new InvalidOperationException($"{owner}: 武将引用为空。");
                if (generals.TryGetValue(def, out var spec)) return spec;
                var unlocks = def.Skills.Select(s => new SkillUnlock(s.Level, Skill(s.Skill, def.name)));
                spec = new GeneralSpec(def.Id, def.DisplayName, def.Troop, def.Force, def.Command, def.Intellect,
                    def.ForceGrowth, def.CommandGrowth, def.IntellectGrowth, unlocks);
                generals[def] = spec;
                return spec;
            }

            var party = Party.Select(g => General(g, name)).ToArray();
            foreach (var g in party)
                if (!troops.Get(g.Troop).PlayerUsable) throw new InvalidOperationException($"{name}: {g.DisplayName} 的兵种 {g.Troop} 只限敌方使用。");
            var encounters = Encounters.Select(e =>
            {
                if (!e) throw new InvalidOperationException($"{name}: 遭遇引用为空。");
                return new EncounterSpec(e.Id, e.DisplayName, e.Enemies.Select(x => new EnemySlot(General(x.General, e.name), x.Level)),
                    e.Ai, e.SkillEveryRounds, e.ExpReward, e.CopperReward);
            }).ToArray();

            Unique(generals.Values.Select(g => g.Id), "武将");
            Unique(skills.Values.Select(s => s.Id), "技能");
            Unique(encounters.Select(e => e.Id), "遭遇");
            return new BattleData(config, troops, new ItemSpec(Ration.Id, Ration.DisplayName, Ration.HealPercent), party, encounters);
        }

        void Unique(IEnumerable<string> ids, string kind)
        {
            var dup = ids.GroupBy(i => i).FirstOrDefault(g => g.Count() > 1);
            if (dup != null) throw new InvalidOperationException($"{name}: {kind} Id「{dup.Key}」重复。");
        }
    }

    public sealed class BattleData
    {
        public BattleConfig Config { get; }
        public TroopTable Troops { get; }
        public ItemSpec Ration { get; }
        public IReadOnlyList<GeneralSpec> Party { get; }
        public IReadOnlyList<EncounterSpec> Encounters { get; }

        public BattleData(BattleConfig config, TroopTable troops, ItemSpec ration, IReadOnlyList<GeneralSpec> party, IReadOnlyList<EncounterSpec> encounters)
        {
            Config = config; Troops = troops; Ration = ration; Party = party; Encounters = encounters;
        }

        public EncounterSpec Encounter(string id) =>
            Encounters.FirstOrDefault(e => e.Id == id) ?? throw new KeyNotFoundException($"没有遭遇 {id}。");
    }
}
