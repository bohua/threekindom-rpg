using System.IO;
using ThreeKingdoms.Core.Battle;
using ThreeKingdoms.Core.Economy;
using ThreeKingdoms.Data.Battle;
using ThreeKingdoms.Data.Economy;
using UnityEditor;
using UnityEngine;

namespace ThreeKingdoms.EditorTools
{
    // Writes the V1 battle data assets from the combat GDD (all values: adopted initial values, untested
    // balance). Idempotent: existing assets are updated in place so their GUIDs and references survive.
    public static class BattleDataBuilder
    {
        const string Root = "Assets/_Project/Data";
        public const string CatalogPath = Root + "/Battle/BattleCatalog.asset";

        [MenuItem("JRPG/Build Battle Data (combat GDD)")]
        public static BattleCatalog Build()
        {
            foreach (var dir in new[] { "Battle", "Characters", "Skills", "Items", "Encounters", "Economy", "Equipment" })
                Directory.CreateDirectory($"{Root}/{dir}");

            var config = Asset<BattleConfigDefinition>("Battle/BattleConfig", c =>
            {
                c.AdvantagePercent = 140; c.GuardPercent = 50; c.MinModifierPercent = 20; c.MaxAttribute = 100;
                c.ExpTable = new[] { 0, 10, 30, 60, 100, 150, 210, 280, 370 };   // 0.2: level cap 5 → 7 (SYSTEMS_V02 §4); 0.3: → 9 (SYSTEMS_V03 §4)
            });
            var troops = Asset<TroopTableDefinition>("Battle/TroopTable", t =>
            {
                t.Types = new[]
                {
                    new TroopTypeEntry { Type = TroopType.Infantry, TroopBonus = 30, DefenseBonus = 5, BaseSpeed = 6, PlayerUsable = true },
                    new TroopTypeEntry { Type = TroopType.Cavalry, TroopBonus = 10, DefenseBonus = 0, BaseSpeed = 10, PlayerUsable = true },
                    new TroopTypeEntry { Type = TroopType.Archer, TroopBonus = 0, DefenseBonus = 0, BaseSpeed = 8, PlayerUsable = true },
                    new TroopTypeEntry { Type = TroopType.YellowTurban, TroopBonus = 20, DefenseBonus = 0, BaseSpeed = 6, PlayerUsable = false },
                    new TroopTypeEntry { Type = TroopType.Strategist, TroopBonus = 0, DefenseBonus = 0, BaseSpeed = 7, PlayerUsable = false },
                };
                t.Advantages = new[]
                {
                    new AdvantagePair { Attacker = TroopType.Cavalry, Defender = TroopType.Infantry },
                    new AdvantagePair { Attacker = TroopType.Infantry, Defender = TroopType.Archer },
                    new AdvantagePair { Attacker = TroopType.Archer, Defender = TroopType.Cavalry },
                    new AdvantagePair { Attacker = TroopType.Cavalry, Defender = TroopType.YellowTurban },
                };
            });
            var ration = Asset<ItemDefinition>("Items/Ration", i => { i.Id = "ration"; i.DisplayName = "军粮"; i.HealPercent = 30; });

            // Our six skills (V1_SPEC §6).
            var regroup = Skill("regroup", "整队", "回复一支存活友军最大兵力的 30%，可对自己使用。", 3, SkillTarget.SingleAlly, DamageKind.None, 0, heal: 30);
            var fireArrows = Skill("fire_arrows", "火矢", "部队火箭战术：对单体敌军造成 1.3 倍计策伤害。", 4, SkillTarget.SingleEnemy, DamageKind.Strategy, 130);
            var charge = Skill("charge", "突击", "对单体敌军造成 1.5 倍物理伤害。", 3, SkillTarget.SingleEnemy, DamageKind.Physical, 150);
            var breakFormation = Skill("break_formation", "破阵", "对单体敌军造成 1.1 倍物理伤害，之后 2 回合其物防 −20%（从下一击起生效）。", 4,
                SkillTarget.SingleEnemy, DamageKind.Physical, 110, stat: BattleStat.PhysicalDefense, mod: -20, turns: 2);
            var sweep = Skill("sweep", "横扫", "对全部敌军各造成 0.65 倍物理伤害。", 4, SkillTarget.AllEnemies, DamageKind.Physical, 65);
            var intimidate = Skill("intimidate", "威慑", "全体敌军物攻 −20%，持续 2 回合。", 4, SkillTarget.AllEnemies, DamageKind.None, 0,
                stat: BattleStat.PhysicalAttack, mod: -20, turns: 2);
            // Enemy skills: same effects as ours, names to be decided (combat GDD Open Question 2) — marked 暂名.
            var enemyCharge = Skill("enemy_assault", "猛攻（暂名）", "效果同突击。", 3, SkillTarget.SingleEnemy, DamageKind.Physical, 150);
            var enemySweep = Skill("enemy_rampage", "乱击（暂名）", "效果同横扫。", 4, SkillTarget.AllEnemies, DamageKind.Physical, 65);
            var enemyRumour = Skill("enemy_rumour", "惑众（暂名）", "号令与谣言动摇军心：全体物攻 −20%，持续 2 回合。效果同威慑。", 4,
                SkillTarget.AllEnemies, DamageKind.None, 0, stat: BattleStat.PhysicalAttack, mod: -20, turns: 2);
            var enemyFire = Skill("enemy_arson", "纵火（暂名）", "指挥部队纵火：单体 1.3 倍计策伤害。效果同火矢。", 4, SkillTarget.SingleEnemy, DamageKind.Strategy, 130);

            // 0.3: Liu Bei's signature skill 众望 (SYSTEMS_V03 §3.6) — a tactics-map craft; the round battles never reach level 8.
            var zhongwang = Skill("zhongwang", "众望", "替相邻友军承受下一次攻击，并提高其物防（随声望增强），持续 2 回合。", 5, SkillTarget.SingleAlly, DamageKind.None, 0);
            var liu = General("liu_bei", "LiuBei", "刘备", TroopType.Archer, 65, 75, 75, 1, 2, 2, (1, regroup), (3, fireArrows), (8, zhongwang));
            var guan = General("guan_yu", "GuanYu", "关羽", TroopType.Cavalry, 90, 80, 60, 2, 2, 1, (1, charge), (3, breakFormation));
            var zhang = General("zhang_fei", "ZhangFei", "张飞", TroopType.Infantry, 95, 75, 45, 3, 2, 1, (1, sweep), (3, intimidate));

            // Enemies: fixed attributes, no growth (combat GDD enemy table).
            var cheng = General("cheng_yuanzhi", "ChengYuanzhi", "程远志", TroopType.YellowTurban, 60, 55, 30, 0, 0, 0);
            var headman = General("yt_headman", "YellowTurbanHeadman", "杂兵头领", TroopType.YellowTurban, 40, 40, 20, 0, 0, 0);
            var officer = General("yt_officer", "YellowTurbanOfficer", "黄巾将领", TroopType.YellowTurban, 55, 45, 30, 0, 0, 0);
            var zhangLiang = General("zhang_liang", "ZhangLiang", "张梁", TroopType.Infantry, 75, 65, 50, 0, 0, 0, (1, enemyCharge));
            var zhangBao = General("zhang_bao", "ZhangBao", "张宝", TroopType.Infantry, 70, 65, 60, 0, 0, 0, (1, enemySweep));
            var zhangJiao = General("zhang_jiao", "ZhangJiao", "张角", TroopType.Strategist, 40, 70, 85, 0, 0, 0, (1, enemyRumour), (1, enemyFire));

            var b1 = Encounter("battle_1", "Battle1_ChengYuanzhi", "第一战 · 程远志来犯", EnemyAiKind.HighestTroops, 10, 30, (cheng, 2), (headman, 1), (headman, 1));
            var b2 = Encounter("battle_2", "Battle2_ZhangLiang", "第二战 · 张梁", EnemyAiKind.FacingSlot, 20, 50, (zhangLiang, 3), (officer, 1), (officer, 1), (officer, 1));
            var b3 = Encounter("battle_3", "Battle3_ZhangBrothers", "第三战 · 张角三兄弟", EnemyAiKind.FacingSlot, 30, 60, (zhangJiao, 4), (zhangBao, 4), (zhangLiang, 4));

            // 0.2 黄巾篇 battles 4–6 (V02_HUANGJIN_PLAN §4; SYSTEMS_V02 §1.4, §2.3). Values are untested initial values.
            var guard = General("yt_guard", "ZhangJiaoGuard", "张角亲卫", TroopType.YellowTurban, 60, 50, 30, 0, 0, 0);
            var dongZhuo = General("dong_zhuo", "DongZhuo", "董卓本阵", TroopType.Infantry, 70, 40, 50, 0, 0, 0);
            var bandit = General("bandit", "Bandit", "山贼喽啰", TroopType.Infantry, 45, 40, 20, 0, 0, 0);
            var banditChief = General("bandit_chief", "BanditChief", "山贼头目", TroopType.Infantry, 72, 55, 30, 0, 0, 0, (1, enemyCharge));
            var b4 = Encounter("battle_4", "Battle4_Yingchuan", "第四战 · 颍川夜战", EnemyAiKind.FacingSlot, 40, 60,
                (zhangBao, 5), (zhangLiang, 5), (officer, 3), (officer, 3));
            b4.Events = new[]
            {
                Event("fire_preview", 2, Objective("坚守！火攻将于下回合发动")),
                Event("fire", 3, Fx("fire_wall"), Banner("火起！"), Dialogue("b4_fire"),
                    new BattleActionEntry { Kind = ScriptActionKind.ApplyStatus, Target = BattleScript.EnemyAll, Text = "chaos", Value = 2 },
                    new BattleActionEntry { Kind = ScriptActionKind.DamageRatio, Target = BattleScript.EnemyAll, Value = 10 },
                    Objective("援军将至：坚持到第 5 回合")),
                Event("cavalry_preview", 4, Objective("红旗骑兵将于下回合杀到")),
                Event("cavalry", 5, Fx("red_cavalry_sweep"), Banner("援军杀到！"), Dialogue("b4_cavalry"),
                    new BattleActionEntry { Kind = ScriptActionKind.Retreat, Target = BattleScript.EnemyAll }),
                Event("opening", 1, Objective("扛住黄巾的攻势")),
            };
            var b5 = Encounter("battle_5", "Battle5_SaveDongZhuo", "第五战 · 广宗救董卓", EnemyAiKind.FacingSlot, 50, 70,
                (zhangJiao, 6), (guard, 4), (guard, 4), (guard, 4));
            b5.Enemies[1].FocusEscort = b5.Enemies[2].FocusEscort = b5.Enemies[3].FocusEscort = true;
            b5.Allies = new[] { new AllyEntry { General = dongZhuo, Level = 7, Escort = true } };
            b5.Events = new[]
            {
                Event("hold", 1, Objective("保护董卓本阵（剩 4 回合）")),
                Event("hold_2", 2, Objective("保护董卓本阵（剩 3 回合）")),
                Event("hold_3", 3, Objective("保护董卓本阵（剩 2 回合）")),
                Event("hold_4", 4, Objective("保护董卓本阵（剩 1 回合）")),
                Event("hold_end", 5, Banner("张角兵败退走"), new BattleActionEntry { Kind = ScriptActionKind.Retreat, Target = BattleScript.EnemyAll }),
                new BattleEventEntry
                {
                    Id = "early_win", When = TriggerKind.UnitHpBelow, Unit = "zhang_jiao", Percent = 30,
                    Actions = new[] { Dialogue("b5_zhangjiao_flee"), new BattleActionEntry { Kind = ScriptActionKind.Retreat, Target = BattleScript.EnemyAll } },
                },
            };
            var b6 = Encounter("battle_6", "Battle6_Bandits", "第六战 · 安喜剿匪", EnemyAiKind.FacingSlot, 60, 80,
                (banditChief, 6), (bandit, 4), (bandit, 4), (bandit, 4));
            b6.Events = new[] { Event("opening", 1, Objective("剿灭山贼")) };

            // 0.3 讨董篇 (V03_TAODONG_PLAN §4): the optional drill with the White Horse riders, Hulao Pass. Both are played on the
            // tactics map; the encounter carries the enemies, levels and rewards.
            var baimaRider = General("baima_rider", "BaimaRider", "白马义从", TroopType.Cavalry, 72, 62, 40, 0, 0, 0, (1, enemyCharge));
            var luBu = General("lu_bu", "LuBu", "吕布", TroopType.Cavalry, 100, 85, 30, 0, 0, 0, (1, enemyCharge), (1, enemySweep));
            var bingzhou = General("bingzhou_cavalry", "BingzhouCavalry", "并州骑", TroopType.Cavalry, 66, 55, 25, 0, 0, 0);
            var b7 = Encounter("battle_7", "Battle7_Drill", "选做 · 校场演武", EnemyAiKind.FacingSlot, 70, 60, (baimaRider, 7), (baimaRider, 7), (baimaRider, 6));
            var b8 = Encounter("battle_8", "Battle8_Hulao", "第八战 · 虎牢关·三英战吕布", EnemyAiKind.FacingSlot, 90, 120, (luBu, 9), (bingzhou, 7), (bingzhou, 7));

            // Equipment catalogue (equipment-items GDD, adopted 2026-10-19; names provisional, values to be tuned).
            var saber = Equip("ring_pommel_saber", "环首刀", EquipSlot.Weapon, TroopType.Infantry, gift: 15, atk: 4);
            var lance = Equip("cavalry_lance", "骑枪", EquipSlot.Weapon, TroopType.Cavalry, gift: 15, atk: 4);
            var huntingBow = Equip("hunting_bow", "猎弓", EquipSlot.Weapon, TroopType.Archer, gift: 15, atk: 4);
            var cloth = Equip("cloth_armor", "布甲", EquipSlot.Armor, null, gift: 10, def: 3);
            var spear = Equip("iron_spear", "精铁长矛", EquipSlot.Weapon, TroopType.Infantry, buy: 60, atk: 9);
            var shuo = Equip("iron_shuo", "精铁马槊", EquipSlot.Weapon, TroopType.Cavalry, buy: 60, atk: 9);
            var hornBow = Equip("horn_bow", "角弓", EquipSlot.Weapon, TroopType.Archer, buy: 60, atk: 8, speed: 1);
            var leather = Equip("leather_armor", "皮甲", EquipSlot.Armor, null, buy: 50, def: 6, troops: 10);
            var bracers = Equip("leather_bracers", "皮护腕", EquipSlot.Accessory, null, buy: 40, def: 3);
            var banner = Equip("war_banner", "军旗", EquipSlot.Accessory, null, buy: 60, troops: 20);
            var manual = Equip("war_manual_fragment", "兵书残卷", EquipSlot.Accessory, null, buy: 80, tech: 2);
            var whip = Equip("swift_whip", "快马鞭", EquipSlot.Accessory, null, gift: 30, speed: 1);   // side-quest reward: not obtainable in Alpha 0.1
            // 0.2: the Anxi market's tier (V02_HUANGJIN_PLAN §4, may be cut) and two story gifts.
            var forgedSaber = Equip("forged_saber", "百炼环首刀", EquipSlot.Weapon, TroopType.Infantry, buy: 130, atk: 13, tier: 2);
            var steelShuo = Equip("steel_shuo", "精钢马槊", EquipSlot.Weapon, TroopType.Cavalry, buy: 130, atk: 13, tier: 2);
            var zheBow = Equip("zhe_longbow", "柘木长弓", EquipSlot.Weapon, TroopType.Archer, buy: 130, atk: 12, speed: 1, tier: 2);
            var lamellar = Equip("iron_lamellar", "铁札甲", EquipSlot.Armor, null, buy: 110, def: 9, troops: 20, tier: 2);
            var mirror = Equip("heart_mirror", "护心镜", EquipSlot.Accessory, null, buy: 90, def: 4, troops: 10, tier: 2);
            var luQuiver = Equip("lu_quiver", "“卢”字箭囊", EquipSlot.Accessory, null, gift: 20, tech: 1, speed: 1);   // corner reward in Lu Zhi's camp
            var woodCharm = Equip("anxi_charm", "护身木牌", EquipSlot.Accessory, null, gift: 10, def: 3);   // thanks of the Anxi townsfolk
            // 0.3: Gongsun Zan's quartermaster (tier 3) and the stable's corner find.
            var yanBow = Equip("yan_horn_bow", "燕地角弓", EquipSlot.Weapon, TroopType.Archer, buy: 210, atk: 16, speed: 1, tier: 3);
            var baimaLance = Equip("baima_lance", "白马骑枪", EquipSlot.Weapon, TroopType.Cavalry, buy: 220, atk: 17, tier: 3);
            var youSpear = Equip("youzhou_spear", "幽州铁矛", EquipSlot.Weapon, TroopType.Infantry, buy: 220, atk: 17, tier: 3);
            var baimaBell = Equip("baima_bell", "白马铃", EquipSlot.Accessory, null, gift: 30, speed: 1, troops: 15);
            var economy = Asset<EconomyCatalog>("Economy/EconomyCatalog", c =>
            {
                c.StartCopper = 120; c.StartRations = 2; c.RationPrice = 10; c.SellPercent = 50;
                c.Equipment = new[] { saber, lance, huntingBow, cloth, spear, shuo, hornBow, leather, bracers, banner, manual, whip,
                    forgedSaber, steelShuo, zheBow, lamellar, mirror, luQuiver, woodCharm, yanBow, baimaLance, youSpear, baimaBell };
                c.StartingGear = new[]
                {
                    new StartingGearEntry { General = liu, Items = new[] { huntingBow, cloth } },
                    new StartingGearEntry { General = guan, Items = new[] { lance, cloth } },
                    new StartingGearEntry { General = zhang, Items = new[] { saber, cloth } },
                };
            });

            var catalog = Asset<BattleCatalog>("Battle/BattleCatalog", c =>
            {
                c.Config = config; c.Troops = troops; c.Ration = ration; c.Economy = economy;
                c.Party = new[] { liu, guan, zhang };
                c.Encounters = new[] { b1, b2, b3, b4, b5, b6, b7, b8 };
            });
            AssetDatabase.SaveAssets();
            catalog.ToRuntime();   // validate
            Debug.Log("BATTLE_DATA_BUILT");
            return catalog;
        }

        public static void BuildFromCommandLine()
        {
            try { Build(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        static T Asset<T>(string path, System.Action<T> fill) where T : ScriptableObject
        {
            string full = $"{Root}/{path}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<T>(full);
            bool created = !asset;
            if (created) asset = ScriptableObject.CreateInstance<T>();
            fill(asset);
            if (created) AssetDatabase.CreateAsset(asset, full);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        static BattleSkillDefinition Skill(string id, string name, string description, int cost, SkillTarget target, DamageKind damage, int power,
            int heal = 0, BattleStat stat = BattleStat.PhysicalAttack, int mod = 0, int turns = 0) =>
            Asset<BattleSkillDefinition>($"Skills/{id}", s =>
            {
                s.Id = id; s.DisplayName = name; s.Description = description; s.TechCost = cost; s.Target = target; s.Damage = damage;
                s.PowerPercent = power; s.HealPercent = heal; s.ModifierStat = stat; s.ModifierPercent = mod; s.ModifierTurns = turns;
            });

        static EquipmentDefinition Equip(string id, string name, EquipSlot slot, TroopType? troop, int buy = 0, int gift = 0,
            int atk = 0, int def = 0, int troops = 0, int tech = 0, int speed = 0, int tier = 1) =>
            Asset<EquipmentDefinition>($"Equipment/{id}", e =>
            {
                e.Id = id; e.DisplayName = name; e.Slot = slot; e.LimitTroop = troop != null; e.Troop = troop ?? TroopType.Infantry;
                e.PhysicalAttack = atk; e.PhysicalDefense = def; e.Troops = troops; e.Tech = tech; e.Speed = speed;
                e.BuyPrice = buy; e.GiftSellPrice = gift; e.ShopTier = tier;
            });

        static GeneralDefinition General(string id, string file, string name, TroopType troop, int force, int command, int intellect,
            int fg, int cg, int ig, params (int level, BattleSkillDefinition skill)[] skills) =>
            Asset<GeneralDefinition>($"Characters/{file}", g =>
            {
                g.Id = id; g.DisplayName = name; g.Troop = troop;
                g.Force = force; g.Command = command; g.Intellect = intellect;
                g.ForceGrowth = fg; g.CommandGrowth = cg; g.IntellectGrowth = ig;
                g.Skills = System.Array.ConvertAll(skills, s => new SkillUnlockEntry { Level = s.level, Skill = s.skill });
            });

        static EncounterDefinition Encounter(string id, string file, string name, EnemyAiKind ai, int exp, int copper,
            params (GeneralDefinition general, int level)[] enemies) =>
            Asset<EncounterDefinition>($"Encounters/{file}", e =>
            {
                e.Id = id; e.DisplayName = name; e.Ai = ai; e.SkillEveryRounds = 3; e.ExpReward = exp; e.CopperReward = copper;
                e.Enemies = System.Array.ConvertAll(enemies, x => new EnemyEntry { General = x.general, Level = x.level });
                e.Allies = System.Array.Empty<AllyEntry>();
                e.Events = System.Array.Empty<BattleEventEntry>();
            });

        static BattleEventEntry Event(string id, int round, params BattleActionEntry[] actions) =>
            new BattleEventEntry { Id = id, When = TriggerKind.TurnStart, Round = round, Actions = actions };

        static BattleActionEntry Objective(string text) => new BattleActionEntry { Kind = ScriptActionKind.SetObjective, Text = text };
        static BattleActionEntry Banner(string text) => new BattleActionEntry { Kind = ScriptActionKind.Banner, Text = text };
        static BattleActionEntry Dialogue(string id) => new BattleActionEntry { Kind = ScriptActionKind.Dialogue, Text = id };
        static BattleActionEntry Fx(string id) => new BattleActionEntry { Kind = ScriptActionKind.Fx, Text = id };
    }
}
