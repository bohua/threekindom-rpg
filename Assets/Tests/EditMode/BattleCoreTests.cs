using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ThreeKingdoms.Core.Battle;

namespace ThreeKingdoms.Tests.EditMode
{
    // Combat GDD data built in code, independent of the assets, so the rule tests pin the GDD numbers.
    static class GddData
    {
        public static BattleConfig Config(int advantage = 140) => new BattleConfig(advantage, 50, 20, 100, new[] { 0, 10, 30, 60, 100 });

        public static readonly TroopTable Troops = new TroopTable(new[]
        {
            new TroopTypeSpec(TroopType.Infantry, 30, 5, 6, true),
            new TroopTypeSpec(TroopType.Cavalry, 10, 0, 10, true),
            new TroopTypeSpec(TroopType.Archer, 0, 0, 8, true),
            new TroopTypeSpec(TroopType.YellowTurban, 20, 0, 6, false),
            new TroopTypeSpec(TroopType.Strategist, 0, 0, 7, false),
        }, new[]
        {
            (TroopType.Cavalry, TroopType.Infantry), (TroopType.Infantry, TroopType.Archer),
            (TroopType.Archer, TroopType.Cavalry), (TroopType.Cavalry, TroopType.YellowTurban),
        });

        public static readonly ItemSpec Ration = new ItemSpec("ration", "军粮", 30);

        public static readonly SkillSpec Regroup = new SkillSpec("regroup", "整队", "", 3, SkillTarget.SingleAlly, DamageKind.None, 0, healPercent: 30);
        public static readonly SkillSpec FireArrows = new SkillSpec("fire_arrows", "火矢", "", 4, SkillTarget.SingleEnemy, DamageKind.Strategy, 130);
        public static readonly SkillSpec Charge = new SkillSpec("charge", "突击", "", 3, SkillTarget.SingleEnemy, DamageKind.Physical, 150);
        public static readonly SkillSpec BreakFormation = new SkillSpec("break_formation", "破阵", "", 4, SkillTarget.SingleEnemy, DamageKind.Physical, 110,
            modifierStat: BattleStat.PhysicalDefense, modifierPercent: -20, modifierTurns: 2);
        public static readonly SkillSpec Sweep = new SkillSpec("sweep", "横扫", "", 4, SkillTarget.AllEnemies, DamageKind.Physical, 65);
        public static readonly SkillSpec Intimidate = new SkillSpec("intimidate", "威慑", "", 4, SkillTarget.AllEnemies, DamageKind.None, 0,
            modifierStat: BattleStat.PhysicalAttack, modifierPercent: -20, modifierTurns: 2);

        public static readonly GeneralSpec LiuBei = new GeneralSpec("liu_bei", "刘备", TroopType.Archer, 65, 75, 75, 1, 2, 2,
            new[] { new SkillUnlock(1, Regroup), new SkillUnlock(3, FireArrows) });
        public static readonly GeneralSpec GuanYu = new GeneralSpec("guan_yu", "关羽", TroopType.Cavalry, 90, 80, 60, 2, 2, 1,
            new[] { new SkillUnlock(1, Charge), new SkillUnlock(3, BreakFormation) });
        public static readonly GeneralSpec ZhangFei = new GeneralSpec("zhang_fei", "张飞", TroopType.Infantry, 95, 75, 45, 3, 2, 1,
            new[] { new SkillUnlock(1, Sweep), new SkillUnlock(3, Intimidate) });

        public static readonly GeneralSpec ChengYuanzhi = new GeneralSpec("cheng_yuanzhi", "程远志", TroopType.YellowTurban, 60, 55, 30);
        public static readonly GeneralSpec Headman = new GeneralSpec("yt_headman", "杂兵头领", TroopType.YellowTurban, 40, 40, 20);
        public static readonly GeneralSpec ZhangLiang = new GeneralSpec("zhang_liang", "张梁", TroopType.Infantry, 75, 65, 50, skills: new[] { new SkillUnlock(1, Charge) });
        public static readonly GeneralSpec ZhangJiao = new GeneralSpec("zhang_jiao", "张角", TroopType.Strategist, 40, 70, 85,
            skills: new[] { new SkillUnlock(1, Intimidate), new SkillUnlock(1, FireArrows) });

        public static EncounterSpec Battle1 => new EncounterSpec("battle_1", "第一战", new[]
        {
            new EnemySlot(ChengYuanzhi, 2), new EnemySlot(Headman, 1), new EnemySlot(Headman, 1),
        }, EnemyAiKind.HighestTroops, 3, 10, 30);

        public static EncounterSpec Single(GeneralSpec enemy, int level, EnemyAiKind ai = EnemyAiKind.FacingSlot) =>
            new EncounterSpec("test", "测试", new[] { new EnemySlot(enemy, level) }, ai, 3, 0, 0);

        public static BattleSession Session(EncounterSpec encounter, IEnumerable<CombatantInput> lineup, int rations = 2, BattleConfig config = null, IEnemyAi ai = null) =>
            new BattleSession(encounter, lineup.ToList(), Ration, rations, config ?? Config(), Troops, ai);

        public static CombatantInput At(GeneralSpec g, int level) => CombatantInput.AtLevel(g, level);
    }

    // An AI that only defends, so a test controls exactly what the enemy side does.
    sealed class PassiveAi : IEnemyAi
    {
        public IReadOnlyList<BattleCommand> Decide(BattleView view) =>
            view.Enemies.Where(u => u.Active).Select(BattleCommand.Defend).ToList();
    }

    sealed class ScriptedAi : IEnemyAi
    {
        readonly System.Func<BattleView, IReadOnlyList<BattleCommand>> decide;
        public ScriptedAi(System.Func<BattleView, IReadOnlyList<BattleCommand>> decide) { this.decide = decide; }
        public IReadOnlyList<BattleCommand> Decide(BattleView view) => decide(view);
    }

    public sealed class BattleCoreTests
    {
        static BattleUnit P(BattleSession s, int slot) => s.State.Players[slot];
        static BattleUnit E(BattleSession s, int slot) => s.State.Enemies[slot];
        static IEnumerable<BattleCommand> DefendAll(BattleSession s) => s.State.Players.Where(u => u.Active).Select(BattleCommand.Defend);

        // ---------- derived stats (GDD Formulas table) ----------

        [Test]
        public void Level1DerivedStatsMatchGddTable()
        {
            var s = GddData.Session(GddData.Battle1, new[] { GddData.At(GddData.LiuBei, 1), GddData.At(GddData.GuanYu, 1), GddData.At(GddData.ZhangFei, 1) });
            var liu = P(s, 0).Stats; var guan = P(s, 1).Stats; var zhang = P(s, 2).Stats;
            Assert.That(new[] { liu.MaxTroops, liu.MaxTech, liu.PhysicalAttack, liu.PhysicalDefense, liu.StrategyAttack, liu.StrategyDefense, liu.Speed }, Is.EqualTo(new[] { 100, 6, 42, 30, 47, 30, 8 }));
            Assert.That(new[] { guan.MaxTroops, guan.MaxTech, guan.PhysicalAttack, guan.PhysicalDefense, guan.StrategyAttack, guan.StrategyDefense, guan.Speed }, Is.EqualTo(new[] { 110, 6, 55, 31, 40, 25, 10 }));
            Assert.That(new[] { zhang.MaxTroops, zhang.MaxTech, zhang.PhysicalAttack, zhang.PhysicalDefense, zhang.StrategyAttack, zhang.StrategyDefense, zhang.Speed }, Is.EqualTo(new[] { 130, 6, 57, 35, 32, 20, 6 }));
        }

        [Test]
        public void EnemyStatsMatchGddTable()
        {
            var s = GddData.Session(GddData.Battle1, new[] { GddData.At(GddData.GuanYu, 1) });
            var cheng = E(s, 0).Stats; var head = E(s, 1).Stats;
            Assert.That(new[] { cheng.MaxTroops, cheng.MaxTech, cheng.PhysicalAttack, cheng.PhysicalDefense, cheng.StrategyAttack, cheng.StrategyDefense, cheng.Speed }, Is.EqualTo(new[] { 140, 8, 42, 24, 27, 16, 6 }));
            Assert.That(new[] { head.MaxTroops, head.MaxTech, head.PhysicalAttack, head.PhysicalDefense, head.StrategyAttack, head.StrategyDefense, head.Speed }, Is.EqualTo(new[] { 120, 6, 30, 18, 20, 11, 6 }));
        }

        // ---------- damage examples ----------

        [Test]
        public void ChargeOnYellowTurbanIs102AndGuarded51()
        {
            var config = GddData.Config();
            Assert.AreEqual(102, BattleRules.Damage(55, 100, 18, 100, 150, advantage: true, guarded: false, config));
            Assert.AreEqual(51, BattleRules.Damage(55, 100, 18, 100, 150, advantage: true, guarded: true, config));
        }

        [Test]
        public void ChargeInBattleUsesCavalryAdvantageOverYellowTurban()
        {
            var s = GddData.Session(GddData.Battle1, new[] { GddData.At(GddData.GuanYu, 1) }, ai: new PassiveAi());
            var log = s.PlayRound(new[] { BattleCommand.UseSkill(P(s, 0), GddData.Charge, E(s, 1)) });
            var hit = log.Single(e => e.Kind == BattleEventKind.Damage);
            Assert.IsTrue(hit.Advantage);
            Assert.IsTrue(hit.Guarded);      // the passive AI defends
            Assert.AreEqual(51, hit.Amount);
        }

        [Test]
        public void FireArrowsUsesStrategyStatsWithoutAdvantage()
        {
            var config = GddData.Config();
            Assert.AreEqual(54, BattleRules.Damage(53, 100, 28, 100, 130, false, false, config));
            var s = GddData.Session(GddData.Single(GddData.ZhangLiang, 4), new[] { GddData.At(GddData.LiuBei, 3) }, ai: new PassiveAi());
            Assert.AreEqual(53, P(s, 0).Stats.StrategyAttack);
        }

        [Test]
        public void IntimidateLowersAttackTo40Point8()
        {
            var config = GddData.Config();
            Assert.AreEqual(21, BattleRules.Damage(51, 80, 38, 100, 100, false, false, config));
            Assert.AreEqual(32, BattleRules.Damage(51, 100, 38, 100, 100, false, false, config));
        }

        [Test]
        public void IntimidateAppliesInBattleAndExpiresAfterSecondRound()
        {
            // Zhang Fei L3 (物防 38) intimidates Zhang Liang L3 (物攻 51), who attacks him.
            var enc = GddData.Single(GddData.ZhangLiang, 3);
            var s = GddData.Session(enc, new[] { GddData.At(GddData.ZhangFei, 3) },
                ai: new ScriptedAi(v => new[] { BattleCommand.Attack(v.Enemies[0], v.Players[0]) }));
            Assert.AreEqual(38, P(s, 0).Stats.PhysicalDefense);
            Assert.AreEqual(51, E(s, 0).Stats.PhysicalAttack);
            // Same speed 6: Zhang Fei (our side) acts first, so the debuff lands before the hit.
            var r1 = s.PlayRound(new[] { BattleCommand.UseSkill(P(s, 0), GddData.Intimidate) });
            Assert.AreEqual(21, r1.Single(e => e.Kind == BattleEventKind.Damage && e.Target == P(s, 0)).Amount);
            Assert.AreEqual(1, E(s, 0).Modifiers.Single().RemainingTurns);
            var r2 = s.PlayRound(new[] { BattleCommand.Defend(P(s, 0)) });
            Assert.AreEqual(10, r2.Single(e => e.Kind == BattleEventKind.Damage).Amount);   // floor(21.8 × 0.5)
            Assert.IsEmpty(E(s, 0).Modifiers);
            Assert.IsTrue(r2.Any(e => e.Kind == BattleEventKind.ModifierExpired));
            var r3 = s.PlayRound(new[] { BattleCommand.Defend(P(s, 0)) });
            Assert.AreEqual(16, r3.Single(e => e.Kind == BattleEventKind.Damage).Amount);   // floor(32 × 0.5)
        }

        [Test]
        public void DamageIsAtLeastOne()
        {
            Assert.AreEqual(1, BattleRules.Damage(10, 100, 200, 100, 65, false, true, GddData.Config()));
        }

        [Test]
        public void AdvantageDirectionsAndStrategistIsNeutral()
        {
            var t = GddData.Troops;
            Assert.IsTrue(t.HasAdvantage(TroopType.Archer, TroopType.Cavalry));
            Assert.IsFalse(t.HasAdvantage(TroopType.Cavalry, TroopType.Archer));
            Assert.IsTrue(t.HasAdvantage(TroopType.Cavalry, TroopType.YellowTurban));
            Assert.IsFalse(t.HasAdvantage(TroopType.YellowTurban, TroopType.Infantry));
            foreach (var other in new[] { TroopType.Infantry, TroopType.Cavalry, TroopType.Archer })
            {
                Assert.IsFalse(t.HasAdvantage(other, TroopType.Strategist));
                Assert.IsFalse(t.HasAdvantage(TroopType.Strategist, other));
            }
        }

        [Test]
        public void AdvantageMultiplierComesFromData()
        {
            Assert.AreEqual(147, BattleRules.Damage(55, 100, 18, 100, 150, true, false, GddData.Config(advantage: 200)));   // 73.5 × 2.0
        }

        // ---------- order ----------

        [Test]
        public void DefendResolvesBeforeFasterAttacker()
        {
            // Zhang Fei (speed 6) defends; Guan Yu-like cavalry enemy (speed 10) attacks him.
            var enemy = new GeneralSpec("cav", "骑兵敌将", TroopType.Cavalry, 80, 60, 40);
            var s = GddData.Session(GddData.Single(enemy, 1), new[] { GddData.At(GddData.ZhangFei, 1) },
                ai: new ScriptedAi(v => new[] { BattleCommand.Attack(v.Enemies[0], v.Players[0]) }));
            var log = s.PlayRound(new[] { BattleCommand.Defend(P(s, 0)) });
            Assert.AreEqual(BattleEventKind.Defend, log[0].Kind);
            var hit = log.Single(e => e.Kind == BattleEventKind.Damage);
            Assert.IsTrue(hit.Guarded);
            Assert.IsTrue(hit.Advantage);   // cavalry beats infantry
        }

        [Test]
        public void EqualSpeedOurSideFirstThenSlot()
        {
            var s = GddData.Session(GddData.Battle1, new[] { GddData.At(GddData.ZhangFei, 1), GddData.At(GddData.GuanYu, 1), GddData.At(GddData.LiuBei, 1) });
            var order = s.State.ActionOrder().Select(u => u.Id).ToArray();
            Assert.That(order, Is.EqualTo(new[] { "guan_yu", "liu_bei", "zhang_fei", "cheng_yuanzhi", "yt_headman", "yt_headman" }));
            Assert.AreEqual(Side.Enemy, s.State.ActionOrder()[4].Side);
            Assert.AreEqual(1, s.State.ActionOrder()[4].Slot);
        }

        // ---------- targets, resources ----------

        [Test]
        public void AttackOnRetreatedTargetSwitchesToFirstLivingEnemy()
        {
            var s = GddData.Session(GddData.Battle1, new[] { GddData.At(GddData.GuanYu, 5), GddData.At(GddData.LiuBei, 5) }, ai: new PassiveAi());
            // Guan Yu (faster) finishes headman 2 over two rounds, Liu Bei targets the same one.
            s.PlayRound(new[] { BattleCommand.UseSkill(P(s, 0), GddData.Charge, E(s, 2)), BattleCommand.Defend(P(s, 1)) });
            var log = s.PlayRound(new[] { BattleCommand.UseSkill(P(s, 0), GddData.Charge, E(s, 2)), BattleCommand.Attack(P(s, 1), E(s, 2)) });
            Assert.IsFalse(E(s, 2).Active);
            var re = log.Single(e => e.Kind == BattleEventKind.Retargeted);
            Assert.AreSame(E(s, 0), re.Target);
        }

        [Test]
        public void SingleSkillWithoutLegalTargetIsCancelledAndKeepsTech()
        {
            var s = GddData.Session(GddData.Battle1, new[] { GddData.At(GddData.GuanYu, 5), GddData.At(GddData.LiuBei, 5) }, ai: new PassiveAi());
            s.PlayRound(new[] { BattleCommand.UseSkill(P(s, 0), GddData.Charge, E(s, 2)), BattleCommand.Defend(P(s, 1)) });
            int tech = P(s, 1).Tech;
            var log = s.PlayRound(new[] { BattleCommand.UseSkill(P(s, 0), GddData.Charge, E(s, 2)), BattleCommand.UseSkill(P(s, 1), GddData.FireArrows, E(s, 2)) });
            Assert.IsFalse(E(s, 2).Active);
            Assert.AreEqual(CancelReason.TargetInvalid, log.Single(e => e.Kind == BattleEventKind.Cancelled && e.Actor == P(s, 1)).Reason);
            Assert.AreEqual(tech, P(s, 1).Tech);
        }

        [Test]
        public void SweepHitsOnlyRemainingEnemiesEachJudgedSeparately()
        {
            var s = GddData.Session(GddData.Battle1, new[] { GddData.At(GddData.GuanYu, 5), GddData.At(GddData.ZhangFei, 1) }, ai: new PassiveAi());
            s.PlayRound(new[] { BattleCommand.UseSkill(P(s, 0), GddData.Charge, E(s, 2)), BattleCommand.Defend(P(s, 1)) });
            var log = s.PlayRound(new[] { BattleCommand.UseSkill(P(s, 0), GddData.Charge, E(s, 2)), BattleCommand.UseSkill(P(s, 1), GddData.Sweep) });
            var sweep = log.Where(e => e.Kind == BattleEventKind.Damage && e.Actor == P(s, 1)).ToList();
            Assert.AreEqual(2, sweep.Count);
            Assert.IsTrue(sweep.All(e => !e.Advantage));   // infantry vs yellow turban: no advantage
        }

        [Test]
        public void RationOnFullTargetIsCancelledAndKept()
        {
            var s = GddData.Session(GddData.Battle1, new[] { GddData.At(GddData.LiuBei, 1) }, rations: 2, ai: new PassiveAi());
            var log = s.PlayRound(new[] { BattleCommand.Ration(P(s, 0), P(s, 0)) });
            Assert.AreEqual(CancelReason.TargetFullTroops, log.Single(e => e.Kind == BattleEventKind.Cancelled).Reason);
            Assert.AreEqual(2, s.State.Rations);
            Assert.IsFalse(s.State.CanUseRation(P(s, 0)));
        }

        [Test]
        public void RationHealsThirtyPercentFloored()
        {
            var s = GddData.Session(GddData.Battle1, new[] { GddData.At(GddData.ZhangFei, 1) }, rations: 1);
            s.PlayRound(new[] { BattleCommand.Defend(P(s, 0)) });   // battle-1 AI attacks him
            int before = P(s, 0).Troops;
            var log = s.PlayRound(new[] { BattleCommand.Ration(P(s, 0), P(s, 0)) });
            var used = log.Single(e => e.Kind == BattleEventKind.ItemUsed);
            Assert.AreEqual(System.Math.Min(39, 130 - before), used.Amount);   // floor(130 × 0.3)
            Assert.AreEqual(0, s.State.Rations);
            Assert.AreEqual(1, s.State.RationsUsed);
        }

        [Test]
        public void UnaffordableSkillIsGreyedAndForcedUseIsCancelled()
        {
            var s = GddData.Session(GddData.Battle1, new[] { GddData.At(GddData.GuanYu, 1) }, ai: new PassiveAi());
            s.PlayRound(new[] { BattleCommand.UseSkill(P(s, 0), GddData.Charge, E(s, 0)) });   // 6 → 3
            Assert.AreEqual(3, P(s, 0).Tech);
            Assert.IsTrue(s.State.CanUseSkill(P(s, 0), GddData.Charge));
            s.PlayRound(new[] { BattleCommand.Attack(P(s, 0), E(s, 0)) });
            s.PlayRound(new[] { BattleCommand.UseSkill(P(s, 0), GddData.Charge, E(s, 1)) });   // 3 → 0
            Assert.IsFalse(s.State.CanUseSkill(P(s, 0), GddData.Charge));
            var log = s.PlayRound(new[] { BattleCommand.UseSkill(P(s, 0), GddData.Charge, E(s, 1)) });
            Assert.AreEqual(CancelReason.NotEnoughTech, log.Single(e => e.Kind == BattleEventKind.Cancelled).Reason);
            Assert.AreEqual(0, P(s, 0).Tech);
        }

        [Test]
        public void TechNeverRegeneratesDuringBattle()
        {
            var s = GddData.Session(GddData.Battle1, new[] { GddData.At(GddData.GuanYu, 1) }, ai: new PassiveAi());
            s.PlayRound(new[] { BattleCommand.UseSkill(P(s, 0), GddData.Charge, E(s, 0)) });
            for (int i = 0; i < 3; i++) s.PlayRound(new[] { BattleCommand.Defend(P(s, 0)) });
            Assert.AreEqual(3, P(s, 0).Tech);
        }

        [Test]
        public void RegroupCanTargetLiuBeiHimself()
        {
            var s = GddData.Session(GddData.Battle1, new[] { GddData.At(GddData.LiuBei, 1) });
            s.PlayRound(new[] { BattleCommand.Defend(P(s, 0)) });
            int before = P(s, 0).Troops;
            Assert.Less(before, 100);
            var log = s.PlayRound(new[] { BattleCommand.UseSkill(P(s, 0), GddData.Regroup, P(s, 0)) });
            var heal = log.Single(e => e.Kind == BattleEventKind.Heal);
            Assert.AreEqual(System.Math.Min(30, 100 - before), heal.Amount);
            Assert.IsFalse(heal.Advantage);
        }

        // ---------- modifiers ----------

        [Test]
        public void BreakFormationAppliesFromNextHitAndDoesNotStack()
        {
            var s = GddData.Session(GddData.Single(GddData.ZhangLiang, 3), new[] { GddData.At(GddData.GuanYu, 3), GddData.At(GddData.ZhangFei, 3) }, ai: new PassiveAi());
            var target = E(s, 0);
            int def = target.Stats.PhysicalDefense;
            var log = s.PlayRound(new[] { BattleCommand.UseSkill(P(s, 0), GddData.BreakFormation, target), BattleCommand.Attack(P(s, 1), target) });
            var hits = log.Where(e => e.Kind == BattleEventKind.Damage).ToList();
            int guanAtk = P(s, 0).Stats.PhysicalAttack, zhangAtk = P(s, 1).Stats.PhysicalAttack;
            var config = GddData.Config();
            Assert.AreEqual(BattleRules.Damage(guanAtk, 100, def, 100, 110, true, true, config), hits[0].Amount);
            Assert.AreEqual(BattleRules.Damage(zhangAtk, 100, def, 80, 100, false, true, config), hits[1].Amount);
            Assert.AreEqual(1, target.Modifiers.Single().RemainingTurns);
            s.PlayRound(new[] { BattleCommand.UseSkill(P(s, 0), GddData.BreakFormation, target), BattleCommand.Defend(P(s, 1)) });
            var m = target.Modifiers.Single();
            Assert.AreEqual(-20, m.Percent);
            Assert.AreEqual(1, m.RemainingTurns);   // refreshed to 2, then −1 at round end
        }

        [Test]
        public void OppositeModifiersSumAndFloorIsPointTwo()
        {
            var s = GddData.Session(GddData.Battle1, new[] { GddData.At(GddData.GuanYu, 1) });
            var u = P(s, 0);
            u.ApplyModifier(BattleStat.PhysicalAttack, 20, 2);
            u.ApplyModifier(BattleStat.PhysicalAttack, -20, 2);
            Assert.AreEqual(100, u.ModifierPercent(BattleStat.PhysicalAttack));
            u.ApplyModifier(BattleStat.PhysicalDefense, -95, 2);
            Assert.AreEqual(20, u.ModifierPercent(BattleStat.PhysicalDefense));
        }

        // ---------- outcome ----------

        [Test]
        public void LastEnemyRetreatEndsRoundImmediatelyWithoutSpending()
        {
            var s = GddData.Session(GddData.Single(GddData.Headman, 1), new[] { GddData.At(GddData.GuanYu, 5), GddData.At(GddData.ZhangFei, 5) }, ai: new PassiveAi());
            s.PlayRound(new[] { BattleCommand.UseSkill(P(s, 0), GddData.Charge, E(s, 0)), BattleCommand.Defend(P(s, 1)) });
            int tech = P(s, 1).Tech;
            var log = s.PlayRound(new[] { BattleCommand.UseSkill(P(s, 0), GddData.Charge, E(s, 0)), BattleCommand.UseSkill(P(s, 1), GddData.Sweep) });
            Assert.AreEqual(BattleOutcome.Victory, s.State.Outcome);
            Assert.AreEqual(BattleEventKind.Victory, log.Last().Kind);
            Assert.AreEqual(tech, P(s, 1).Tech);
            Assert.IsFalse(log.Any(e => e.Actor == P(s, 1) && e.Kind != BattleEventKind.Defend));
        }

        [Test]
        public void LastPlayerRetreatIsImmediateDefeatAndRetreatedUnitsCannotBeHealed()
        {
            var s = GddData.Session(GddData.Single(GddData.ZhangLiang, 5, EnemyAiKind.FacingSlot), new[] { GddData.At(GddData.LiuBei, 1) }, rations: 2);
            BattleUnit liu = P(s, 0);
            while (s.Result == null) s.PlayRound(new[] { BattleCommand.Attack(liu, E(s, 0)) });
            Assert.AreEqual(BattleOutcome.Defeat, s.Result.Outcome);
            Assert.AreEqual(0, s.Result.Exp);
            Assert.AreEqual(0, s.Result.RationsUsed);
            Assert.IsEmpty(s.State.LegalTargets(liu, CommandKind.Item));
        }

        [Test]
        public void EnemyOnlyTroopTypeIsRejectedInLineup()
        {
            Assert.Throws<System.ArgumentException>(() => GddData.Session(GddData.Battle1, new[] { GddData.At(GddData.ChengYuanzhi, 1) }));
            Assert.Throws<System.ArgumentException>(() => GddData.Session(GddData.Battle1, new CombatantInput[0]));
        }

        [Test]
        public void ExpTableGivesLevels2Then3Then4()
        {
            var config = GddData.Config();
            Assert.AreEqual(2, BattleRules.LevelForExp(10, config));
            Assert.AreEqual(3, BattleRules.LevelForExp(30, config));
            Assert.AreEqual(4, BattleRules.LevelForExp(60, config));
            Assert.AreEqual(5, BattleRules.LevelForExp(1000, config));
            Assert.AreEqual(new[] { "regroup", "fire_arrows" }, GddData.LiuBei.SkillsAt(3).Select(x => x.Id).ToArray());
        }

        // ---------- enemy AI ----------

        [Test]
        public void Battle1AiAttacksHighestTroops()
        {
            var s = GddData.Session(GddData.Battle1, new[] { GddData.At(GddData.LiuBei, 1), GddData.At(GddData.GuanYu, 1), GddData.At(GddData.ZhangFei, 1) });
            var log = s.PlayRound(DefendAll(s));
            var enemyHits = log.Where(e => e.Kind == BattleEventKind.Damage && e.Actor.Side == Side.Enemy).ToList();
            Assert.AreEqual(3, enemyHits.Count);
            Assert.IsTrue(enemyHits.All(e => e.Target == P(s, 2)));   // Zhang Fei, 130 troops
        }

        [Test]
        public void AiDecisionDoesNotDependOnPlayerCommands()
        {
            string Run(System.Func<BattleSession, IEnumerable<BattleCommand>> mine)
            {
                var s = GddData.Session(GddData.Battle1, new[] { GddData.At(GddData.LiuBei, 1), GddData.At(GddData.GuanYu, 1), GddData.At(GddData.ZhangFei, 1) });
                var log = s.PlayRound(mine(s));
                return string.Join(";", log.Where(e => e.Kind == BattleEventKind.ActionStarted && e.Actor.Side == Side.Enemy).Select(e => $"{e.Actor.Slot}>{e.Target.Slot}"));
            }
            Assert.AreEqual(Run(DefendAll), Run(s => s.State.Players.Select(p => BattleCommand.Attack(p, s.State.Enemies[0]))));
        }

        [Test]
        public void FacingSlotAiUsesSkillsOnRound3And6Alternating()
        {
            var view = GddData.Session(new EncounterSpec("b3", "第三战", new[] { new EnemySlot(GddData.ZhangJiao, 4) }, EnemyAiKind.FacingSlot, 3, 30, 60),
                new[] { GddData.At(GddData.LiuBei, 3), GddData.At(GddData.GuanYu, 3) }, ai: new PassiveAi());
            var ai = new FacingSlotAi(3);
            var s = view;
            var picks = new List<string>();
            for (int round = 1; round <= 6; round++)
            {
                var cmd = ai.Decide(new BattleView(s.State)).Single();
                picks.Add(cmd.Kind == CommandKind.Skill ? cmd.Skill.Id : "attack:" + cmd.Target.Slot);
                s.PlayRound(DefendAll(s));
            }
            Assert.That(picks, Is.EqualTo(new[] { "attack:0", "attack:0", "intimidate", "attack:0", "attack:0", "fire_arrows" }));
        }

        [Test]
        public void FacingSlotAiFallsBackToWeakestWhenFacingRetreated()
        {
            var enc = new EncounterSpec("b2", "第二战", new[] { new EnemySlot(GddData.Headman, 1), new EnemySlot(GddData.Headman, 1), new EnemySlot(GddData.Headman, 1) }, EnemyAiKind.FacingSlot, 3, 0, 0);
            var s = GddData.Session(enc, new[] { GddData.At(GddData.LiuBei, 1), GddData.At(GddData.ZhangFei, 1) }, ai: new PassiveAi());
            var cmds = new FacingSlotAi(3).Decide(new BattleView(s.State));
            Assert.That(cmds.Select(c => c.Target.Slot), Is.EqualTo(new[] { 0, 1, 1 }));   // extra enemy takes our last slot
            P(s, 0).TakeDamage(1000);
            P(s, 1).TakeDamage(5);
            cmds = new FacingSlotAi(3).Decide(new BattleView(s.State));
            Assert.That(cmds.Select(c => c.Target.Slot), Is.EqualTo(new[] { 1, 1, 1 }));
        }

        // ---------- whole battle ----------

        // A fixed, simple player script for battle 1: Guan Yu charges while he can, others attack the
        // lowest-troop enemy; Liu Bei regroups whoever drops under half.
        static IEnumerable<BattleCommand> Script(BattleSession s)
        {
            var targets = s.State.Enemies.Where(e => e.Active).OrderBy(e => e.Troops).ThenBy(e => e.Slot).ToList();
            foreach (var p in s.State.Players.Where(u => u.Active))
            {
                var hurt = s.State.Players.Where(u => u.Active && u.Troops * 2 < u.Stats.MaxTroops).OrderBy(u => u.Troops).FirstOrDefault();
                if (p.Id == "liu_bei" && hurt != null && s.State.CanUseSkill(p, GddData.Regroup)) yield return BattleCommand.UseSkill(p, GddData.Regroup, hurt);
                else if (p.Id == "guan_yu" && s.State.CanUseSkill(p, GddData.Charge)) yield return BattleCommand.UseSkill(p, GddData.Charge, targets[0]);
                else yield return BattleCommand.Attack(p, targets[0]);
            }
        }

        static (BattleResult result, string log) PlayBattle1()
        {
            var s = GddData.Session(GddData.Battle1, new[] { GddData.At(GddData.LiuBei, 1), GddData.At(GddData.GuanYu, 1), GddData.At(GddData.ZhangFei, 1) });
            var all = new List<string>();
            for (int guard = 0; s.Result == null && guard < 50; guard++)
                all.AddRange(s.PlayRound(Script(s).ToList()).Select(e => e.ToString()));
            return (s.Result, string.Join("\n", all));
        }

        [Test]
        public void Battle1ThreeVsThreeIsDeterministicVictory()
        {
            var a = PlayBattle1();
            var b = PlayBattle1();
            Assert.IsNotNull(a.result);
            Assert.AreEqual(BattleOutcome.Victory, a.result.Outcome);
            Assert.AreEqual("battle_1", a.result.EncounterId);
            Assert.AreEqual(10, a.result.Exp);
            Assert.AreEqual(30, a.result.Copper);
            Assert.AreEqual(a.log, b.log);
            Assert.LessOrEqual(a.result.Rounds, 6);
        }
    }
}
