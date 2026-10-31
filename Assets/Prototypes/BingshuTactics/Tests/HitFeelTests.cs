// 打击感档位解析（design/hit-feel/HIT_FEEL_DESIGN.md 第 2 节）：纯函数，只读命中事件
using NUnit.Framework;
using Prototype.BingshuTactics;
using UnityEngine;

public class HitFeelTests
{
    static HitEvent E(HitSource src = HitSource.Attack, int dmg = 30) => new HitEvent
    {
        Attacker = "关羽", Target = "黄巾兵", AttackerTeam = Team.Player, TargetTeam = Team.Enemy, Source = src, Damage = dmg
    };

    [Test]
    public void PlainAttack_IsNormal() => Assert.AreEqual(HitTier.Normal, HitFeel.Resolve(E()));

    [Test]
    public void PositiveReasons_AndTechniques_AreHeavy()
    {
        var troop = E(); troop.TroopAdvantage = true; troop.AdvMul = 1.3f;
        var element = E(HitSource.Strategy); element.ElementAdvantage = true; element.AdvMul = 1.2f;
        var height = E(); height.HeightMul = 1.1f;
        Assert.AreEqual(HitTier.Heavy, HitFeel.Resolve(troop));
        Assert.AreEqual(HitTier.Heavy, HitFeel.Resolve(element));
        Assert.AreEqual(HitTier.Heavy, HitFeel.Resolve(height));
        Assert.AreEqual(HitTier.Heavy, HitFeel.Resolve(E(HitSource.Technique)));
        Assert.AreEqual("压制", troop.Reason);
        Assert.AreEqual("居高", height.Reason);
        // 敌打我：兵种压制不算重击，武技仍算
        var enemyTroop = E(); enemyTroop.AttackerTeam = Team.Enemy; enemyTroop.TargetTeam = Team.Player; enemyTroop.TroopAdvantage = true; enemyTroop.AdvMul = 1.3f;
        Assert.AreEqual(HitTier.Normal, HitFeel.Resolve(enemyTroop));
        var enemyTech = E(HitSource.Technique); enemyTech.AttackerTeam = Team.Enemy; enemyTech.TargetTeam = Team.Player;
        Assert.AreEqual(HitTier.Heavy, HitFeel.Resolve(enemyTech));
    }

    [Test]
    public void Disadvantage_OrFlooredToOne_IsDull()
    {
        var bad = E(); bad.AdvMul = 0.9f;
        var low = E(); low.HeightMul = 0.9f;
        Assert.AreEqual(HitTier.Dull, HitFeel.Resolve(bad));
        Assert.AreEqual(HitTier.Dull, HitFeel.Resolve(low));
        Assert.AreEqual(HitTier.Dull, HitFeel.Resolve(E(dmg: 1)));
        // 取满足的最高一档：劣势但击破仍是击破
        bad.Broke = true;
        Assert.AreEqual(HitTier.Break, HitFeel.Resolve(bad));
    }

    [Test]
    public void Breaks_NamedLeaders_AndTheLastBlow_AreRouts()
    {
        var plain = E(); plain.Broke = true;
        var leader = E(); leader.Broke = true; leader.NamedTarget = true;
        var last = E(); last.Broke = true; last.LastBlow = true;
        var boss = E(); boss.Floored = true; boss.BossTarget = true; boss.Damage = 0;   // 不动如山
        Assert.AreEqual(HitTier.Break, HitFeel.Resolve(plain));
        Assert.AreEqual(HitTier.Rout, HitFeel.Resolve(leader));
        Assert.AreEqual(HitTier.Rout, HitFeel.Resolve(last));
        Assert.AreEqual(HitTier.Rout, HitFeel.Resolve(boss));
    }

    [Test]
    public void Duel_HasItsOwnTiers()
    {
        var d = E(HitSource.Duel); Assert.AreEqual(HitTier.Duel, HitFeel.Resolve(d));
        d.LastBlow = true; Assert.AreEqual(HitTier.DuelFinal, HitFeel.Resolve(d));
    }

    [Test]
    public void EnemyHittingUs_ShakesOneTierLess_ExceptBossSignatures()
    {
        var e = E(); e.AttackerTeam = Team.Enemy; e.TargetTeam = Team.Player;
        Assert.AreEqual(HitTier.Normal, HitFeel.ShakeTier(e, HitTier.Heavy));
        Assert.AreEqual(HitTier.Heavy, HitFeel.ShakeTier(E(), HitTier.Heavy));
        var sig = E(HitSource.Technique); sig.AttackerTeam = Team.Enemy; sig.TargetTeam = Team.Player; sig.BossAttacker = true;
        Assert.AreEqual(HitTier.Heavy, HitFeel.ShakeTier(sig, HitTier.Heavy));
        var loss = E(); loss.TargetTeam = Team.Player; loss.Broke = true;
        Assert.IsTrue(HitFeel.FriendlyLoss(loss));
    }

    [Test]
    public void Profile_FollowsTheTable()
    {
        var p = ScriptableObject.CreateInstance<HitFeelProfile>();
        Assert.AreEqual(33, p.For(HitTier.Normal).StopMs);
        Assert.AreEqual(50, p.For(HitTier.Heavy).StopMs);
        Assert.AreEqual(83, p.For(HitTier.Break).StopMs);
        Assert.AreEqual(133, p.For(HitTier.Rout).StopMs);
        Assert.AreEqual(0, p.For(HitTier.Normal).ShakePx, "普通绝不震屏");
        Assert.AreEqual(2, p.For(HitTier.Heavy).ShakePx);
        Assert.LessOrEqual(p.For(HitTier.Normal).FlashBright, 0.9f, "普通闪白不触发泛光");
        Assert.IsTrue(p.For(HitTier.Rout).SecondFlashGold);
        foreach (var row in p.Tiers) Assert.LessOrEqual(row.SquashPx / p.SourcePxHeight, p.MaxDeform + 1e-4f, row.Tier + " 形变上限 10%");
        Object.DestroyImmediate(p);
    }
}
