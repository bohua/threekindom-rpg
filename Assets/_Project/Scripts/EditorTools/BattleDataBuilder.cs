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
                c.ExpTable = new[] { 0, 10, 30, 60, 100, 150, 210, 280, 370, 480, 610, 760, 930, 1120, 1330 };   // 0.2: level cap 5 → 7 (SYSTEMS_V02 §4); 0.3: → 9 (SYSTEMS_V03 §4); 0.4: → 12 (SYSTEMS_V04 §6); 0.5: → 15 (SYSTEMS_V05 §6)
            });
            var troops = Asset<TroopTableDefinition>("Battle/TroopTable", t =>
            {
                // BALANCE_MODEL_v1 §3.14 (user 2026-10-27): 8 main + 6 special types; nobody changes type, ranks come with level.
                // Every type is player-usable: 贼众 stay 贼众 when they join after surrendering (GENERALS_ROSTER §4).
                TroopTypeEntry T(TroopType type, int troops, int def, int speed) =>
                    new TroopTypeEntry { Type = type, TroopBonus = troops, DefenseBonus = def, BaseSpeed = speed, PlayerUsable = true };
                t.Types = new[]
                {
                    T(TroopType.Infantry, 30, 5, 6), T(TroopType.Cavalry, 10, 0, 10), T(TroopType.Archer, 0, 0, 8),
                    T(TroopType.YellowTurban, 20, 0, 6), T(TroopType.Strategist, 0, 0, 7), T(TroopType.Spearman, 20, 3, 6),
                    T(TroopType.Crossbow, 10, 2, 7), T(TroopType.HorseArcher, 0, 0, 10),
                    T(TroopType.Swordswoman, 10, 0, 8), T(TroopType.Dancer, 0, 0, 7), T(TroopType.Shaman, 0, 0, 7),
                    T(TroopType.Pirate, 20, 2, 7), T(TroopType.Barbarian, 35, 3, 6), T(TroopType.Elephant, 40, 5, 6),
                };
            });
            var ration = Asset<ItemDefinition>("Items/Ration", i => { i.Id = "ration"; i.DisplayName = "军粮"; i.HealPercent = 30; });

            // Our six skills (V1_SPEC §6).
            var regroup = Skill("regroup", "整队", "回复一支存活友军最大兵力的 30%，可对自己使用。", 3, SkillTarget.SingleAlly, DamageKind.None, 0, heal: 30);
            var fireArrows = Skill("fire_arrows", "流矢", "部队箭雨战术：对单体敌军造成 1.3 倍计策伤害。", 4, SkillTarget.SingleEnemy, DamageKind.Strategy, 130);
            var charge = Skill("charge", "突击", "对单体敌军造成 1.5 倍物理伤害。", 3, SkillTarget.SingleEnemy, DamageKind.Physical, 150);
            var breakFormation = Skill("break_formation", "破阵", "对单体敌军造成 1.1 倍物理伤害，之后 2 回合其物防 −20%（从下一击起生效）。", 4,
                SkillTarget.SingleEnemy, DamageKind.Physical, 110, stat: BattleStat.PhysicalDefense, mod: -20, turns: 2);
            var sweep = Skill("sweep", "横扫", "对全部敌军各造成 0.8 倍物理伤害。", 4, SkillTarget.AllEnemies, DamageKind.Physical, 80);   // BALANCE_MODEL_v1 C15: 0.65 → 0.8
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
            // The third signature skills at level 10 (BALANCE_MODEL_v1 §3.9): tactics-map crafts; the round battles never reach them.
            var tuoDao = Skill("tuo_dao", "拖刀", "近战单体 1.6 倍物理伤害，出手后更快再动。", 5, SkillTarget.SingleEnemy, DamageKind.Physical, 160);
            var duanQiao = Skill("duan_qiao", "断桥", "近战单体 1.7 倍物理伤害，并推迟其行动。", 5, SkillTarget.SingleEnemy, DamageKind.Physical, 170);
            // GENERALS_ROSTER v0.3 (user 2026-10-27): fixed attributes, no growth; Liu Bei leads infantry, Guan Yu and Zhang Fei cavalry.
            var liu = E(General("liu_bei", "LiuBei", "刘备", TroopType.Infantry, 68, 78, 76, 0, 0, 0, (1, regroup), (3, fireArrows), (8, zhongwang)), UnitElement.Water);
            var guan = E(General("guan_yu", "GuanYu", "关羽", TroopType.Cavalry, 96, 88, 70, 0, 0, 0, (1, charge), (3, breakFormation), (10, tuoDao)), UnitElement.Fire);
            var zhang = E(General("zhang_fei", "ZhangFei", "张飞", TroopType.Cavalry, 97, 78, 40, 0, 0, 0, (1, sweep), (3, intimidate), (10, duanQiao)), UnitElement.Earth);

            // Enemies: fixed attributes, no growth (combat GDD enemy table).
            var cheng = E(General("cheng_yuanzhi", "ChengYuanzhi", "程远志", TroopType.YellowTurban, 62, 55, 30, 0, 0, 0), UnitElement.Earth);
            var headman = E(General("yt_headman", "YellowTurbanHeadman", "杂兵头领", TroopType.YellowTurban, 40, 40, 20, 0, 0, 0), UnitElement.Earth);
            var officer = E(General("yt_officer", "YellowTurbanOfficer", "黄巾将领", TroopType.YellowTurban, 55, 45, 30, 0, 0, 0), UnitElement.Wood);
            var zhangLiang = E(General("zhang_liang", "ZhangLiang", "张梁", TroopType.YellowTurban, 72, 60, 48, 0, 0, 0, (1, enemyCharge)), UnitElement.Earth);
            var zhangBao = E(General("zhang_bao", "ZhangBao", "张宝", TroopType.Shaman, 70, 62, 55, 0, 0, 0, (1, enemySweep)), UnitElement.Earth);
            var zhangJiao = E(General("zhang_jiao", "ZhangJiao", "张角", TroopType.Strategist, 40, 70, 85, 0, 0, 0, (1, enemyRumour), (1, enemyFire)), UnitElement.Earth);

            var b1 = Encounter("battle_1", "Battle1_ChengYuanzhi", "第一战 · 程远志来犯", EnemyAiKind.HighestTroops, 10, 30, (cheng, 2), (headman, 1), (headman, 1));
            var b2 = Encounter("battle_2", "Battle2_ZhangLiang", "第二战 · 张梁", EnemyAiKind.FacingSlot, 20, 50, (zhangLiang, 3), (officer, 1), (officer, 1), (officer, 1));
            var b3 = Encounter("battle_3", "Battle3_ZhangBrothers", "第三战 · 张角三兄弟", EnemyAiKind.FacingSlot, 30, 60, (zhangJiao, 4), (zhangBao, 4), (zhangLiang, 4));

            // 0.2 黄巾篇 battles 4–6 (V02_HUANGJIN_PLAN §4; SYSTEMS_V02 §1.4, §2.3). Values are untested initial values.
            var guard = E(General("yt_guard", "ZhangJiaoGuard", "张角亲卫", TroopType.YellowTurban, 60, 50, 30, 0, 0, 0), UnitElement.Wood);
            var dongZhuo = E(General("dong_zhuo", "DongZhuo", "董卓本阵", TroopType.Infantry, 70, 40, 50, 0, 0, 0), UnitElement.Fire);
            var bandit = E(General("bandit", "Bandit", "山贼喽啰", TroopType.YellowTurban, 45, 40, 20, 0, 0, 0), UnitElement.Metal);
            var banditChief = E(General("bandit_chief", "BanditChief", "山贼头目", TroopType.YellowTurban, 70, 50, 25, 0, 0, 0, (1, enemyCharge)), UnitElement.Metal);
            var b4 = Encounter("battle_4", "Battle4_Yingchuan", "第四战 · 颍川夜战", EnemyAiKind.FacingSlot, 40, 60,
                (zhangBao, 5), (zhangLiang, 5), (officer, 2), (officer, 2));   // BALANCE_MODEL_v1 §3.11 (D8): officers 2 2, the two extras 1 1
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
                (banditChief, 6), (bandit, 4), (bandit, 4));   // §3.11: five with the two extras at 3
            b6.Events = new[] { Event("opening", 1, Objective("剿灭山贼")) };

            // 0.3 讨董篇 (V03_TAODONG_PLAN §4): the optional drill with the White Horse riders, Hulao Pass. Both are played on the
            // tactics map; the encounter carries the enemies, levels and rewards.
            var baimaRider = E(General("baima_rider", "BaimaRider", "白马义从", TroopType.HorseArcher, 72, 62, 40, 0, 0, 0, (1, enemyCharge)), UnitElement.Fire);
            var luBu = E(General("lu_bu", "LuBu", "吕布", TroopType.Cavalry, 100, 88, 25, 0, 0, 0, (1, enemyCharge), (1, enemySweep)), UnitElement.Fire);
            var bingzhou = E(General("bingzhou_cavalry", "BingzhouCavalry", "并州骑", TroopType.Cavalry, 66, 55, 25, 0, 0, 0), UnitElement.Fire);
            var b7 = Encounter("battle_7", "Battle7_Drill", "选做 · 校场演武", EnemyAiKind.FacingSlot, 70, 60, (baimaRider, 5), (baimaRider, 5), (baimaRider, 4), (baimaRider, 4));   // §3.11: 弓骑 5 5 4 4
            // 0.4 河北篇 (V04_HEBEI_PLAN §4): Pingyuan's raiders, Wen Chou at the Panhe, Qu Yi's vanguard and crossbows at Jieqiao.
            var raiderChief = E(General("liukou_chief", "RaiderChief", "流寇头目", TroopType.YellowTurban, 72, 55, 28, 0, 0, 0, (1, enemyCharge)), UnitElement.Metal);
            var raider = E(General("liukou", "Raider", "流寇", TroopType.YellowTurban, 52, 44, 20, 0, 0, 0), UnitElement.Metal);
            var wenChou = E(General("wen_chou", "WenChou", "文丑", TroopType.Cavalry, 92, 72, 30, 0, 0, 0, (1, enemyCharge), (1, enemySweep)), UnitElement.Metal);
            var yuanCav = E(General("yuan_cavalry", "YuanCavalry", "袁军骑", TroopType.Cavalry, 66, 56, 25, 0, 0, 0), UnitElement.Metal);
            var quYi = E(General("qu_yi", "QuYi", "麴义", TroopType.Crossbow, 78, 86, 60, 0, 0, 0, (1, enemyCharge)), UnitElement.Wood);
            var xiandeng = E(General("xiandeng", "Xiandeng", "先登死士", TroopType.Spearman, 72, 76, 20, 0, 0, 0), UnitElement.Wood);
            var crossbow = E(General("qiangnu", "Crossbow", "强弩手", TroopType.Crossbow, 70, 50, 30, 0, 0, 0), UnitElement.Metal);
            var b9 = Encounter("battle_9", "Battle9_Pingyuan", "第九战 · 平原御寇", EnemyAiKind.FacingSlot, 90, 100,
                (raiderChief, 9), (raider, 7), (raider, 7), (raider, 6), (raider, 6));
            var b10 = Encounter("battle_10", "Battle10_Panhe", "第十战 · 磐河救公孙瓒", EnemyAiKind.FacingSlot, 110, 120,
                (wenChou, 11), (yuanCav, 9), (yuanCav, 9), (yuanCav, 8), (yuanCav, 8));
            var b11 = Encounter("battle_11", "Battle11_Jieqiao", "第十一战 · 界桥殿后", EnemyAiKind.FacingSlot, 130, 150,
                (quYi, 11), (xiandeng, 10), (xiandeng, 10), (crossbow, 9), (crossbow, 9));
            // 0.5 徐州篇 (V05_XUZHOU_PLAN §4): Guan Hai at Duchang; Cao's army east of Xiapi and at its walls; the Xiaopei patrol.
            var guanHai = E(General("guan_hai", "GuanHai", "管亥", TroopType.YellowTurban, 82, 62, 22, 0, 0, 0, (1, enemyCharge), (1, enemySweep)), UnitElement.Earth);
            var ytRemnant = E(General("yt_remnant", "YellowRemnant", "黄巾余部", TroopType.YellowTurban, 64, 52, 24, 0, 0, 0), UnitElement.Wood);
            var caoInf = E(General("cao_infantry", "CaoInfantry", "曹军步卒", TroopType.Spearman, 70, 64, 30, 0, 0, 0), UnitElement.Fire);
            var caoCav = E(General("cao_cavalry", "CaoCavalry", "曹军骑兵", TroopType.Cavalry, 72, 60, 28, 0, 0, 0), UnitElement.Fire);
            var caoArcher = E(General("cao_archer", "CaoArcher", "曹军弓手", TroopType.Archer, 66, 56, 34, 0, 0, 0), UnitElement.Fire);
            var yuJin = E(General("yu_jin", "YuJin", "于禁", TroopType.Infantry, 76, 86, 68, 0, 0, 0, (1, enemyCharge)), UnitElement.Earth);
            var caoRen = E(General("cao_ren", "CaoRen", "曹仁", TroopType.Cavalry, 82, 88, 60, 0, 0, 0, (1, enemyCharge), (1, enemySweep)), UnitElement.Fire);
            var deserter = E(General("deserter", "Deserter", "散兵", TroopType.Infantry, 66, 54, 24, 0, 0, 0), UnitElement.Fire);
            var b12 = Encounter("battle_12", "Battle12_Duchang", "第十二战 · 都昌解围", EnemyAiKind.FacingSlot, 170, 180,   // §3.10 (C18): 0.5 exp 170 / 190 / 210 / 140
                (guanHai, 12), (ytRemnant, 11), (ytRemnant, 11), (ytRemnant, 10), (ytRemnant, 10), (ytRemnant, 9));
            var b13 = Encounter("battle_13", "Battle13_XiapiEast", "第十三战 · 下邳城东", EnemyAiKind.FacingSlot, 190, 200,
                (caoInf, 11), (caoInf, 10), (caoArcher, 10), (caoArcher, 10));   // §3.11: 枪弓 4 人
            var b14 = Encounter("battle_14", "Battle14_XiapiWalls", "第十四战 · 下邳保卫", EnemyAiKind.FacingSlot, 210, 220,
                (caoRen, 14), (caoCav, 12), (caoCav, 12), (caoCav, 11), (caoCav, 11));
            var b15 = Encounter("battle_15", "Battle15_Xiaopei", "选做 · 小沛巡防", EnemyAiKind.FacingSlot, 140, 150,
                (deserter, 13), (deserter, 13), (raider, 12), (raider, 12), (deserter, 12), (deserter, 11));
            var b8 = Encounter("battle_8", "Battle8_Hulao", "第八战 · 虎牢关·三英战吕布", EnemyAiKind.FacingSlot, 90, 120, (luBu, 9), (bingzhou, 4), (bingzhou, 4), (bingzhou, 4));   // §3.11: Floor 0.5 instead of extra troops

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
                    new StartingGearEntry { General = liu, Items = new[] { saber, cloth } },   // 刘备 leads infantry since v1.3
                    new StartingGearEntry { General = guan, Items = new[] { lance, cloth } },
                    new StartingGearEntry { General = zhang, Items = new[] { lance, cloth } },   // 张飞 leads cavalry since v1.3
                };
            });

            var catalog = Asset<BattleCatalog>("Battle/BattleCatalog", c =>
            {
                c.Config = config; c.Troops = troops; c.Ration = ration; c.Economy = economy;
                c.Party = new[] { liu, guan, zhang };
                c.Encounters = new[] { b1, b2, b3, b4, b5, b6, b7, b8, b9, b10, b11, b12, b13, b14, b15 };
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

        static GeneralDefinition E(GeneralDefinition g, UnitElement element)
        {
            g.Element = element;
            EditorUtility.SetDirty(g);
            return g;
        }

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
