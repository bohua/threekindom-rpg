// PROTOTYPE - NOT FOR PRODUCTION
// Question: 空轨式 AT 行动条＋小范围格子战斗，配合“学派回路镶嵌兵书→策略”，是否好玩、是否看得懂？
// Date: 2026-10-22
using System.Linq;
using NUnit.Framework;
using Prototype.BingshuTactics;
using UnityEngine;
using Grid = Prototype.BingshuTactics.Grid;
using Terrain = Prototype.BingshuTactics.Terrain;

public class TacticsRulesTests
{
    static Book B(Element e, int g) => new Book("测试", e, g);
    static bool Has(System.Collections.Generic.List<LitStrategy> l, string n) => l.Any(x => x.Def.Name == n);

    [Test]
    public void FiveSchools_HaveDistinctAffinityAndLayouts()
    {
        Assert.AreEqual(5, Circuits.All.Length);
        CollectionAssert.AreEquivalent(Wuxing.All, Circuits.All.Select(c => c.Affinity));
        Assert.AreEqual(Element.Water, Circuits.Of(School.Ru).Affinity);
        Assert.AreEqual(Element.Fire, Circuits.Of(School.Bing).Affinity);
        Assert.AreEqual(Element.Wood, Circuits.Of(School.Dao).Affinity);
        Assert.AreEqual(Element.Metal, Circuits.Of(School.Mo).Affinity);
        Assert.AreEqual(Element.Earth, Circuits.Of(School.Fa).Affinity);
        // 中枢只能放本学派偏向五行（用户 2026-10-23）
        foreach (var c in Circuits.All)
        {
            Assert.AreEqual(-1, c.Slots[0].Gua, c.Name);
            Assert.AreEqual(c.Affinity, c.Slots[0].Locked, c.Name);
            Assert.AreEqual(c.Slots.Length, c.Slots.Select(x => x.Gua).Distinct().Count(), c.Name + " 卦位不重复");
        }
        // 包容性：道家除中枢外全自由且自由位最多，法家自由位最少
        Assert.AreEqual(Circuits.Of(School.Dao).Slots.Length - 1, Circuits.Of(School.Dao).FreeSlots);
        Assert.AreEqual(Circuits.All.Max(c => c.FreeSlots), Circuits.Of(School.Dao).FreeSlots);
        Assert.AreEqual(Circuits.All.Min(c => c.FreeSlots), Circuits.Of(School.Fa).FreeSlots);
        foreach (var c in Circuits.All)
            foreach (var line in c.Lines) Assert.IsTrue(line.All(i => i >= 0 && i < c.Slots.Length), c.Name);
    }

    [Test]
    public void LockedSlot_RejectsOtherElements()
    {
        var bing = Circuits.Of(School.Bing);
        Assert.IsTrue(CircuitMath.Fits(bing, 0, B(Element.Fire, 1)));
        Assert.IsFalse(CircuitMath.Fits(bing, 0, B(Element.Water, 3)));
        Assert.IsTrue(CircuitMath.Fits(bing, 1, B(Element.Water, 3)));
    }

    [Test]
    public void AffinityBook_GetsPlusOne()
    {
        Assert.AreEqual(4, CircuitMath.Value(Circuits.Of(School.Ru), B(Element.Water, 3)));
        Assert.AreEqual(3, CircuitMath.Value(Circuits.Of(School.Bing), B(Element.Water, 3)));
    }

    [Test]
    public void Strategy_NeedsTotalsOnOneLine_NotAcrossLines()
    {
        var dao = Circuits.Of(School.Dao);   // 中枢—巽离一线 0-1-2，外圈两段 3-4、5-6
        var slots = new Book[7];
        slots[1] = B(Element.Fire, 2); slots[3] = B(Element.Fire, 2);   // 火分在两条线上，各 2
        var lit = CircuitMath.Lit(dao, slots, 60);
        Assert.IsTrue(Has(lit, "引火"));
        Assert.IsFalse(Has(lit, "火烧营寨"));    // 合计 4 但不在同一条线
        slots[0] = B(Element.Wood, 1); slots[2] = B(Element.Fire, 2);   // 中枢（锁木）只在第一条线上：火4、木2
        lit = CircuitMath.Lit(dao, slots, 60);
        Assert.IsTrue(Has(lit, "火烧营寨"));
    }

    [Test]
    public void Overflow_RaisesPower_AndIsCapped()
    {
        var bing = Circuits.Of(School.Bing);
        var slots = new Book[6];
        slots[0] = B(Element.Fire, 1);        // 火 2
        var l1 = CircuitMath.Lit(bing, slots, 100).First(x => x.Def.Name == "引火");
        Assert.AreEqual(1f, l1.PowerMul, 1e-4);
        slots[2] = B(Element.Fire, 3); slots[4] = B(Element.Fire, 3); slots[1] = B(Element.Fire, 3);   // 火 2+4+4+4=14
        var l2 = CircuitMath.Lit(bing, slots, 100).First(x => x.Def.Name == "引火");
        // 主稿 v1.3 §3.7：一阶溢出封顶 2 点，盈余每点咏唱 −5（最多 3 点）
        Assert.AreEqual(1f + CircuitMath.OverflowStep * CircuitMath.TierOneCap, l2.PowerMul, 1e-4);
        Assert.AreEqual(15, l2.CastCut);
    }

    [Test]
    public void Damage_TroopAndElementAdvantage_Add_AndCap()
    {
        var cav = new Unit { Troop = Troop.Cavalry, Atk = 60, Mat = 60 };
        var rebel = new Unit { Troop = Troop.Rebel, Element = Element.Earth, Def = 20, Mdef = 20 };
        Assert.AreEqual(50, Combat.Damage(cav, rebel, 1f, true, Element.None) / 1.3f, 1f);
        Assert.AreEqual(1.5f, Combat.AdvMul(cav, rebel, Element.Wood), 1e-4);   // 骑压步（贼众）+0.3 ＋木克土 +0.2
        Assert.AreEqual(1.0f, Combat.AdvMul(new Unit { Troop = Troop.Infantry }, rebel, Element.Fire), 1e-4);
    }

    [Test]
    public void Defend_HalvesDamage_GuardTakesStrongest_MinFloor()
    {
        var a = new Unit { Troop = Troop.Archer, Atk = 50 };
        var t = new Unit { Troop = Troop.Archer, Def = 0 };
        int normal = Combat.Damage(a, t, 1f, true, Element.None);
        t.Defending = true;
        Assert.AreEqual(normal / 2, Combat.Damage(a, t, 1f, true, Element.None));
        t.AddStatus(new Status { Name = "阵", Guard = 0.35f, Turns = 2 });
        Assert.AreEqual(0.5f, Combat.TakenMul(t), 1e-4);   // 只取最强，不叠加
        t.Defending = false;
        Assert.AreEqual(0.65f, Combat.TakenMul(t), 1e-4);
    }

    [Test]
    public void Reachable_IsBlockedByRocksAndUnits()
    {
        var g = new Grid(5, 5);
        g.Blocked[1, 0] = true;
        var r = g.Reachable(Vector2Int.zero, 2, p => p == new Vector2Int(0, 1));
        Assert.AreEqual(1, r.Count);   // 右边石头、上边有人
        r = g.Reachable(Vector2Int.zero, 3, p => false);
        Assert.IsTrue(r.ContainsKey(new Vector2Int(1, 1)));
        Assert.AreEqual(3, Grid.Path(r, new Vector2Int(1, 1)).Count);
    }

    [Test]
    public void Areas_HaveExpectedSizes()
    {
        var g = new Grid(20, 20);
        var c = new Vector2Int(10, 10);
        Assert.AreEqual(5, g.Diamond(c, 1).Count());
        Assert.AreEqual(13, g.Diamond(c, 2).Count());
        Assert.AreEqual(21, g.LineArea(c, Vector2Int.right, 7, 3).Count());
        Assert.IsTrue(g.LineArea(c, Vector2Int.up, 5, 1).All(p => p.x == 10 && p.y > 10));
    }

    [Test]
    public void AtClock_FasterAndLighterActsSooner()
    {
        Assert.Less(AtClock.Delay(100, 10), AtClock.Delay(100, 6));
        Assert.Less(AtClock.Delay(40, 8), AtClock.Delay(110, 8));
        var a = new Unit { Team = Team.Enemy, Hp = 1, At = 50 };
        var b = new Unit { Team = Team.Player, Hp = 1, At = 50 };
        var dead = new Unit { Team = Team.Player, Hp = 0, At = 1 };
        Assert.AreSame(b, AtClock.Next(new[] { a, b, dead }));   // 同刻我方先；撤退者不排
    }

    [Test]
    public void Statuses_SameNameRefreshes_AndExpire()
    {
        var u = new Unit();
        u.AddStatus(new Status { Name = "威慑", AtkMul = 0.8f, Turns = 2 });
        u.AddStatus(new Status { Name = "威慑", AtkMul = 0.8f, Turns = 2 });
        Assert.AreEqual(1, u.Statuses.Count);
        u.AddStatus(new Status { Name = "鼓舞", AtkMul = 1.2f, Turns = 1 });
        Assert.AreEqual(0.96f, u.AtkMul, 1e-4);
        u.TickStatuses();
        Assert.AreEqual(0.8f, u.AtkMul, 1e-4);
        u.TickStatuses();
        Assert.AreEqual(1f, u.AtkMul, 1e-4);
    }

    [Test]
    public void Intelligence_SetsSegmentCap_AndFullBoardAtTop()
    {
        Assert.AreEqual(2, CircuitMath.SegmentCap(30));
        Assert.AreEqual(3, CircuitMath.SegmentCap(48));     // 张飞
        Assert.AreEqual(4, CircuitMath.SegmentCap(63));     // 关羽
        Assert.AreEqual(5, CircuitMath.SegmentCap(81));     // 刘备
        var bing = Circuits.Of(School.Bing);                 // 一条六位绳路
        var segs = CircuitMath.Segments(bing, 63, out var edges);
        CollectionAssert.AreEqual(new[] { 4, 2 }, segs.Select(x => x.Length).ToArray());
        Assert.AreEqual(1, edges.Count(e => e.Line < 0));   // 一处断开
        Assert.AreEqual(3, CircuitMath.Segments(bing, 30).Length);
        foreach (var c in Circuits.All)
        {
            var full = CircuitMath.Segments(c, 100, out var fe);
            Assert.AreEqual(1, full.Length, c.Name);
            Assert.AreEqual(c.Slots.Length, full[0].Length, c.Name);
            Assert.IsTrue(fe.All(e => e.Line == 0), c.Name);
            // 每个槽都在某段里，任何智力下都不丢槽
            foreach (int intel in new[] { 10, 48, 63, 81, 95 })
                CollectionAssert.AreEquivalent(Enumerable.Range(0, c.Slots.Length), CircuitMath.Segments(c, intel).SelectMany(x => x).Distinct(), c.Name + intel);
        }
    }

    [Test]
    public void LowIntelligence_CutsHighTierStrategy()
    {
        var bing = Circuits.Of(School.Bing);
        var slots = new Book[6];
        slots[0] = B(Element.Fire, 3); slots[1] = B(Element.Fire, 2); slots[2] = B(Element.Wood, 2); slots[4] = B(Element.Fire, 1);
        // 全线：火 4+3+2=9、木 2 → 火海；智力48每段3槽：首段 火7 木2 仍可火海；智力30每段2槽：火海不成
        Assert.IsTrue(CircuitMath.Lit(bing, slots, 100).Any(x => x.Def.Name == "火海"));
        Assert.IsTrue(CircuitMath.Lit(bing, slots, 48).Any(x => x.Def.Name == "火海"));
        Assert.IsFalse(CircuitMath.Lit(bing, slots, 30).Any(x => x.Def.Name == "火海"));
    }

    [Test]
    public void Terrain_CostsAndHeight()
    {
        var g = Grid.FromRows(new[] { "^A..", ".,=~" }, TacticsGame.Legend);   // 上行 y=1：草1层、草2层；下行 y=0：草、沙、浅滩、河
        var o = new Vector2Int(0, 0);
        Assert.AreEqual(2, g.StepCost(o, new Vector2Int(1, 0), Troop.Infantry));     // 沙地 2
        Assert.AreEqual(3, g.StepCost(o, new Vector2Int(1, 0), Troop.Cavalry));      // 骑兵 3
        Assert.AreEqual(2, g.StepCost(new Vector2Int(1, 0), new Vector2Int(2, 0), Troop.Infantry));   // 浅滩 2
        Assert.AreEqual(-1, g.StepCost(new Vector2Int(2, 0), new Vector2Int(3, 0), Troop.Infantry));  // 河流不可过
        Assert.AreEqual(2, g.StepCost(o, new Vector2Int(0, 1), Troop.Infantry));     // 上一层 +1
        Assert.AreEqual(-1, g.StepCost(new Vector2Int(1, 0), new Vector2Int(1, 1), Troop.Infantry));  // 高差两层不能直接走
        Assert.AreEqual(1, g.StepCost(new Vector2Int(1, 1), new Vector2Int(0, 1), Troop.Infantry));   // 下坡不加
        var r = g.Reachable(o, 2, p => false);
        Assert.IsTrue(r.ContainsKey(new Vector2Int(1, 0)));
        Assert.IsFalse(r.ContainsKey(new Vector2Int(2, 0)));   // 沙2＋浅滩2 超出移动力
        Assert.AreEqual(1.2f, Combat.HeightMul(3, 0), 1e-4);   // 封顶 +20%
        Assert.AreEqual(0.9f, Combat.HeightMul(0, 1), 1e-4);
        var t = new Unit();
        Assert.AreEqual(0.85f, Combat.TakenMul(t, true), 1e-4);   // 营寨
        t.Defending = true;
        Assert.AreEqual(0.5f, Combat.TakenMul(t, true), 1e-4);    // 只取最强
    }

    [Test]
    public void Hulao_OnlyOneTileCanFaceLuBu()
    {
        var g = TacticsGame.HulaoGrid();
        var lubu = new Vector2Int(7, 10);
        Assert.AreEqual(Terrain.Road, g.TerrainAt(new Vector2Int(7, 11)));   // 门道空出一格
        Assert.AreEqual(Terrain.Gate, g.TerrainAt(new Vector2Int(7, 12)));
        var open = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right }
            .Select(d => lubu + d).Where(p => g.In(p) && !Grid.Impassable(g.TerrainAt(p))).ToList();
        CollectionAssert.AreEquivalent(new[] { new Vector2Int(7, 9), new Vector2Int(7, 11) }, open);
        // 从南面出阵区能走到吕布面前（过桥或浅滩）
        var r = g.Reachable(new Vector2Int(7, 1), 99, p => p == lubu);
        Assert.IsTrue(r.ContainsKey(new Vector2Int(7, 9)));
        Assert.IsFalse(r.ContainsKey(new Vector2Int(7, 11)));   // 门道被拒马封住，绕不到吕布背后
        Assert.IsTrue(r.ContainsKey(new Vector2Int(3, 5)));      // 浅滩可渡
        Assert.IsFalse(r.ContainsKey(new Vector2Int(4, 12)));    // 关墙上去不了
        Assert.AreEqual(3, g.HeightAt(new Vector2Int(4, 12)));
    }

    [Test]
    public void CursorDirections_AreDistinctForEveryCameraAngle()
    {
        foreach (float yaw in new[] { 45f, 135f, 225f, 315f, 405f })
        {
            var up = Grid.ScreenDirToGrid(yaw, Vector2Int.up);
            var down = Grid.ScreenDirToGrid(yaw, Vector2Int.down);
            var left = Grid.ScreenDirToGrid(yaw, Vector2Int.left);
            var right = Grid.ScreenDirToGrid(yaw, Vector2Int.right);
            Assert.AreEqual(-up, down, $"yaw {yaw}");
            Assert.AreEqual(-left, right, $"yaw {yaw}");
            Assert.AreEqual(0, up.x * right.x + up.y * right.y, $"yaw {yaw} 上与右应垂直");
            Assert.AreEqual(1, Mathf.Abs(up.x) + Mathf.Abs(up.y));
        }
    }

    [Test]
    public void Duel_RockPaperScissors_AndSymmetric()
    {
        Assert.IsTrue(DuelRules.Beats(DuelMove.Guard, DuelMove.Attack));
        Assert.IsTrue(DuelRules.Beats(DuelMove.Special, DuelMove.Guard));
        Assert.IsTrue(DuelRules.Beats(DuelMove.Attack, DuelMove.Special));
        var (toA, toB, key) = DuelRules.Resolve(DuelMove.Guard, DuelMove.Attack, 95, 95);
        Assert.AreEqual(0, toA); Assert.Greater(toB, 0); Assert.AreEqual("挡下", key);
        (toA, toB, key) = DuelRules.Resolve(DuelMove.Attack, DuelMove.Guard, 95, 95);
        Assert.Greater(toA, 0); Assert.AreEqual(0, toB); Assert.AreEqual("挡下", key);
        (toA, toB, key) = DuelRules.Resolve(DuelMove.Guard, DuelMove.Guard, 95, 95);
        Assert.AreEqual(0, toA + toB); Assert.AreEqual("相持", key);
        // 破防比互搏更痛；武力高的一方打得更重
        Assert.Greater(DuelRules.Resolve(DuelMove.Special, DuelMove.Guard, 95, 95).toB, DuelRules.Resolve(DuelMove.Attack, DuelMove.Attack, 95, 95).toB);
        var trade = DuelRules.Resolve(DuelMove.Attack, DuelMove.Attack, 68, 100);
        Assert.Greater(trade.toA, trade.toB);
    }

    [Test]
    public void Zoc_StopsMovement_AndAlliesArePassable()
    {
        var g = new Grid(7, 1);
        var start = new Vector2Int(0, 0);
        // 友军在 (1,0)：可以穿过，到达更远的格子
        var r = g.Reachable(start, 4, p => false);
        Assert.IsTrue(r.ContainsKey(new Vector2Int(3, 0)));
        Assert.AreEqual(new Vector2Int(1, 0), Grid.Path(r, new Vector2Int(2, 0))[1]);
        // 敌人在 (4,0)：(3,0) 在其 ZOC 内，进入即停，到不了 (4,0) 之后
        var enemy = new Vector2Int(4, 0);
        r = g.Reachable(start, 6, p => p == enemy, Troop.Infantry, p => Grid.Dist(p, enemy) == 1);
        Assert.IsTrue(r.ContainsKey(new Vector2Int(3, 0)));
        Assert.IsFalse(r.ContainsKey(new Vector2Int(5, 0)));
        // 起点就在 ZOC 内也能离开
        r = g.Reachable(new Vector2Int(3, 0), 2, p => p == enemy, Troop.Infantry, p => Grid.Dist(p, enemy) == 1);
        Assert.IsTrue(r.ContainsKey(new Vector2Int(1, 0)));
    }

    [Test]
    public void DefBuff_MultipliesWithDebuff()
    {
        var u = new Unit();
        u.AddStatus(new Status { Name = "昂扬", AtkMul = 1.5f, DefMul = 1.5f, Turns = 3 });
        u.AddStatus(new Status { Name = "破阵", DefMul = 0.8f, Turns = 2 });
        Assert.AreEqual(1.2f, u.DefMul, 1e-4);
        Assert.AreEqual(1.5f, u.AtkMul, 1e-4);
    }

    static Book K(Element e, int g) => new Book("功法", e, g, BookKind.Technique);

    [Test]
    public void Hub_LinksToExactlyOneDirection()
    {
        // 用户 2026-10-26：每个武将的命盘里，有且只有一个方位可以连到八卦中心点
        foreach (var c in Circuits.All)
        {
            var neighbours = c.Lines.SelectMany(line => line.Select((s, i) => (s, i))
                .Where(x => x.s == 0).SelectMany(x => new[] { x.i > 0 ? line[x.i - 1] : -1, x.i < line.Length - 1 ? line[x.i + 1] : -1 }))
                .Where(n => n >= 0).Distinct().ToList();
            Assert.AreEqual(1, neighbours.Count, c.Name);
            Assert.AreEqual(1, c.Lines.Count(line => line.Contains(0)), c.Name + " 中枢只在一条绳路上");
        }
    }

    [Test]
    public void TechniqueBooks_DoNotCountForStrategies_AndStrategyBooksMakeNoTechniques()
    {
        var bing = Circuits.Of(School.Bing);
        var slots = new Book[6];
        slots[0] = K(Element.Fire, 3);
        Assert.IsFalse(Has(CircuitMath.Lit(bing, slots, 100), "引火"), "功法书不给策略计点");
        Assert.AreEqual("猛击", CircuitMath.Techniques(bing, slots, 100).Single().Def.Name, "一本功法自成小武技");
        slots[0] = B(Element.Fire, 3);
        Assert.IsTrue(Has(CircuitMath.Lit(bing, slots, 100), "引火"));
        Assert.IsEmpty(CircuitMath.Techniques(bing, slots, 100), "策略书不出武技");
    }

    [Test]
    public void AdjacentTechniqueBooks_MergeIntoBigTechnique_WhenSameElementReachesThree()
    {
        var ru = Circuits.Of(School.Ru);                 // 外圈一段 离4–巽5–震6
        var slots = new Book[7];
        slots[4] = K(Element.Metal, 2); slots[5] = K(Element.Metal, 1);
        var t = CircuitMath.Techniques(ru, slots, 81);
        Assert.AreEqual("突阵", t.Single().Def.Name, "相邻两本金系合计 3 → 大武技（主稿 §3.8 门槛 3）");
        slots[4] = K(Element.Metal, 1); slots[5] = K(Element.Metal, 1);
        CollectionAssert.AreEquivalent(new[] { "刺击" }, CircuitMath.Techniques(ru, slots, 81).Select(x => x.Def.Name), "合计 2 不足：各出小武技（同名只留一招）");
        slots[4] = K(Element.Earth, 2); slots[5] = K(Element.Fire, 2);
        CollectionAssert.AreEquivalent(new[] { "格挡", "猛击" }, CircuitMath.Techniques(ru, slots, 81).Select(x => x.Def.Name), "异五行相邻不并组（C10）");
        slots[4] = K(Element.Metal, 2);
        slots[5] = B(Element.Metal, 3);
        Assert.AreEqual(1, CircuitMath.Techniques(ru, slots, 81).Count, "中间隔着策略书就不相连");
    }

    [Test]
    public void Bookcase_DefaultSocketsFitTheirCircuits_AndDoNotShareBooks()
    {
        var lib = Bookcase.NewLibrary();
        var used = new System.Collections.Generic.HashSet<Book>();
        foreach (var name in new[] { "刘备", "关羽", "张飞" })
        {
            var c = Circuits.Of(Bookcase.SchoolOf(name));
            var s = new Book[7];
            Bookcase.Default(name, s, lib);
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == null) continue;
                Assert.IsTrue(CircuitMath.Fits(c, i, s[i]), $"{name} 槽{i} {s[i].Name}");
                Assert.IsTrue(used.Add(s[i]), $"{s[i].Name} 被两人同时装备");
            }
            Assert.IsTrue(s.Any(b => b != null && b.IsTechnique), name + " 开局带一本功法书");
        }
    }
}
