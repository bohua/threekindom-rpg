using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Prototype.BingshuTactics;
using ThreeKingdoms.Gameplay.Story;
using ThreeKingdoms.UI.Battle;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// 打击感不改数值的验证（design/hit-feel/HIT_FEEL_DESIGN.md 第 6 节第 9 条）：电脑代打同一场战斗、同一种子，
// 打击感开、关各跑一次，逐行比对 TACTICS 战斗日志与每一次命中（伤害、行动顺序、胜负必须一致）。
// 档位比例统计（100 场以上）较慢，只在设了环境变量 HITFEEL_STATS（报告文件路径）时运行。
public sealed class HitFeelComparisonTests
{
    [SetUp] public void Fresh()
    {
        GameRun.Clear();
        StagePlayer.AutoSeconds = 0f;
        ChapterTactics.AutoPlay = true;
        Time.timeScale = 12f;
    }

    [TearDown] public void Clean()
    {
        ChapterTactics.AutoPlay = false;
        Time.timeScale = 1f;
        Application.targetFrameRate = -1;
        HitFeelSettings.Enabled = true;
        HitFeelLayers.Reset();
        TacticsGame.SeedOverride = null;
        TacticsGame.BattleLogged = null;
        TacticsGame.HitLogged = null;
        TacticsGame.FlashCounted = null;
        TacticsGame.ShowTimeAdded = null;
        TacticsGame.ForceFullShows = false;
        TacticsGame.Chapter = null;
        GameRun.Clear();
    }

    sealed class Run
    {
        public readonly List<string> Log = new List<string>();
        public readonly Dictionary<HitTier, int> Tiers = new Dictionary<HitTier, int>();
        public readonly Dictionary<string, int> HeavyWhy = new Dictionary<string, int>();   // 重击的原因组合
        public bool Won; public int Rounds;
    }

    static float lastStart, lastEnd;   // 本场战斗从开场到结束的游戏时间

    static IEnumerator Play(string scene, int seed, bool feel, Run run)
    {
        GameRun.Clear();   // 每场都是全新的开发档：上一场的经验不会带进下一场
        HitFeelSettings.Enabled = feel;
        TacticsGame.SeedOverride = seed;
        TacticsGame.BattleLogged = l => run.Log.Add(l);
        TacticsGame.HitLogged = (e, t) => { run.Log.Add("HIT " + e); run.Tiers[t] = run.Tiers.TryGetValue(t, out int n) ? n + 1 : 1;
            if (t == HitTier.Heavy) { string k = Why(e); run.HeavyWhy[k] = run.HeavyWhy.TryGetValue(k, out int m) ? m + 1 : 1; } };
        SceneManager.LoadScene(scene);
        float deadline = Time.realtimeSinceStartup + 400f;
        TacticsGame game = null;
        while (game == null) { Assert.Less(Time.realtimeSinceStartup, deadline, "waiting for " + scene); yield return null; game = Object.FindFirstObjectByType<TacticsGame>(); }
        yield return null;
        lastStart = Time.time;
        Application.targetFrameRate = -1;   // 不限帧率只让代打跑得快；战斗结果不依赖帧率
        while (!game.ChapterFinished && !game.Lost)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, scene + " did not finish");
            yield return null;
        }
        lastEnd = Time.time;
        run.Won = game.Won; run.Rounds = game.Rounds;
        run.Log.Add($"END won={run.Won} rounds={run.Rounds}");
        TacticsGame.BattleLogged = null; TacticsGame.HitLogged = null;
        SceneManager.LoadScene(GameRun.TitleScene);
        yield return null; yield return null;
    }

    static string Why(HitEvent e) => string.Join("+", new[]
    {
        e.Source == HitSource.Technique ? "武技" : null, e.TroopAdvantage ? "兵种" : null, e.ElementAdvantage ? "五行" : null, e.HeightMul > 1.001f ? "居高" : null,
        e.AttackerTeam == Team.Enemy ? "敌打我" : null,
    }.Where(x => x != null));

    static void AssertSame(Run a, Run b, string what)
    {
        int n = Mathf.Min(a.Log.Count, b.Log.Count);
        for (int i = 0; i < n; i++)
            Assert.AreEqual(a.Log[i], b.Log[i], $"{what}: line {i + 1} differs");
        Assert.AreEqual(a.Log.Count, b.Log.Count, what + ": log length");
    }

    [UnityTest, Timeout(1800000)]
    public IEnumerator HulaoPass_SameSeed_SameBattle_WithOrWithoutHitFeel()
    {
        var on = new Run(); var off = new Run(); var again = new Run();
        yield return Play("Battle_Hulao08", 7, true, on);
        yield return Play("Battle_Hulao08", 7, false, off);
        yield return Play("Battle_Hulao08", 7, true, again);
        Debug.Log($"HITFEEL_COMPARE battle_8 seed 7: {on.Log.Count} lines, {on.Log.Count(l => l.StartsWith("HIT "))} hits, won={on.Won} round {on.Rounds}; tiers "
                  + string.Join(" ", on.Tiers.OrderBy(k => k.Key).Select(k => $"{HitFeel.Name(k.Key)}{k.Value}")));
        Assert.Greater(on.Log.Count(l => l.StartsWith("HIT ")), 10, "the AI actually fought");
        AssertSame(on, again, "hit feel on, run twice");
        AssertSame(on, off, "hit feel on vs off");
    }

    [UnityTest, Timeout(1800000)]
    public IEnumerator Yingchuan_SameSeed_SameBattle_WithOrWithoutHitFeel()
    {
        var on = new Run(); var off = new Run();
        yield return Play("Battle_Yingchuan04", 3, true, on);
        yield return Play("Battle_Yingchuan04", 3, false, off);
        Debug.Log($"HITFEEL_COMPARE battle_4 seed 3: {on.Log.Count} lines, won={on.Won} round {on.Rounds}");
        AssertSame(on, off, "hit feel on vs off");
    }

    static readonly string[] StatScenes =
    {
        "Battle_Yingchuan04", "Battle_Guangzong05", "Battle_Anxi06", "Battle_Drill07", "Battle_Hulao08", "Battle_Pingyuan09",
        "Battle_Panhe10", "Battle_Jieqiao11", "Battle_Duchang12", "Battle_XiapiEast13", "Battle_XiapiWalls14", "Battle_Xiaopei15",
    };

    // 100 场以上电脑代打：各档出现比例（目标：重击约 ≤30% 的命中）与胜率
    [UnityTest, Timeout(14400000)]
    public IEnumerator TierShares_Over100Battles()
    {
        string report = System.Environment.GetEnvironmentVariable("HITFEEL_STATS");
        if (string.IsNullOrEmpty(report)) { Assert.Ignore("set HITFEEL_STATS=<report file> to run the 100-battle statistics"); yield break; }
        int seeds = int.TryParse(System.Environment.GetEnvironmentVariable("HITFEEL_SEEDS"), out int sn) ? sn : 9;
        var why = new Dictionary<string, int>();
        var total = new Dictionary<HitTier, int>();
        var rows = new List<string>();
        int battles = 0, wins = 0;
        foreach (var scene in StatScenes)
        {
            var sceneTiers = new Dictionary<HitTier, int>(); int sceneWins = 0;
            for (int seed = 1; seed <= seeds; seed++)
            {
                var r = new Run();
                yield return Play(scene, seed, true, r);
                battles++; if (r.Won) { wins++; sceneWins++; }
                foreach (var kv in r.HeavyWhy) why[kv.Key] = why.TryGetValue(kv.Key, out int w) ? w + kv.Value : kv.Value;
                foreach (var kv in r.Tiers)
                {
                    total[kv.Key] = total.TryGetValue(kv.Key, out int a) ? a + kv.Value : kv.Value;
                    sceneTiers[kv.Key] = sceneTiers.TryGetValue(kv.Key, out int b) ? b + kv.Value : kv.Value;
                }
            }
            rows.Add($"| {scene} | {sceneWins}/{seeds} | {Shares(sceneTiers)} |");
        }
        int hits = total.Values.Sum();
        string text = $"# 打击感档位比例（{battles} 场电脑代打，{hits} 次命中）\n\n胜率 {wins}/{battles}\n\n全部：{Shares(total)}\n\n| 战斗 | 胜 | 各档比例 |\n| --- | --- | --- |\n" + string.Join("\n", rows) + "\n\n重击原因组合：" + string.Join("，", why.OrderByDescending(k => k.Value).Select(k => $"{k.Key} {k.Value}")) + "\n";
        System.IO.File.WriteAllText(report, text);
        Debug.Log("HITFEEL_STATS\n" + text);
        Assert.GreaterOrEqual(battles, seeds >= 9 ? 100 : 12);
    }

    // 节奏预算与光敏（BATTLE_VFX_DESIGN 2.6、2.7）：完整版设置（每次完整、代打也完整）下 100 场以上电脑代打，
    // 演出增量 ≤ 战斗时长 15%，全屏每秒闪白 ≤ 3 次。较慢，只在设了环境变量 HITFEEL_BUDGET（报告文件路径）时运行。
    [UnityTest, Timeout(14400000)]
    public IEnumerator ShowBudget_Over100Battles()
    {
        string report = System.Environment.GetEnvironmentVariable("HITFEEL_BUDGET");
        if (string.IsNullOrEmpty(report)) { Assert.Ignore("set HITFEEL_BUDGET=<report file> to run the 100-battle pacing budget"); yield break; }
        int seeds = int.TryParse(System.Environment.GetEnvironmentVariable("HITFEEL_SEEDS"), out int sn) ? sn : 9;
        int keepCutin = HitFeelSettings.Cutin;
        HitFeelSettings.Cutin = 2; TacticsGame.ForceFullShows = true;
        double showAll = 0, durAll = 0, worstRatio = 0; int battles = 0, worstFlashes = 0, overWindows = 0, flashesAll = 0; string worstBattle = "";
        var rows = new List<string>();
        foreach (var scene in StatScenes)
        {
            double sceneShow = 0, sceneDur = 0; int sceneFlashMax = 0;
            for (int seed = 1; seed <= seeds; seed++)
            {
                double show = 0; var flashes = new List<float>();
                TacticsGame.ShowTimeAdded = dt => show += dt;
                TacticsGame.FlashCounted = t => flashes.Add(t);
                var r = new Run();
                yield return Play(scene, seed, true, r);
                double dur = lastEnd - lastStart;
                battles++; showAll += show; durAll += dur; sceneShow += show; sceneDur += dur; flashesAll += flashes.Count;
                double ratio = show / System.Math.Max(1e-3, dur - show);
                if (ratio > worstRatio) { worstRatio = ratio; worstBattle = $"{scene} 种子 {seed}"; }
                for (int i = 0; i < flashes.Count; i++)
                {
                    int n = flashes.Count(f => f >= flashes[i] && f < flashes[i] + 1f);
                    if (n > 3) overWindows++;
                    sceneFlashMax = Mathf.Max(sceneFlashMax, n); worstFlashes = Mathf.Max(worstFlashes, n);
                }
            }
            rows.Add($"| {scene} | {sceneShow / System.Math.Max(1e-3, sceneDur - sceneShow) * 100:0.0}% | {sceneFlashMax} |");
        }
        HitFeelSettings.Cutin = keepCutin;
        double all = showAll / System.Math.Max(1e-3, durAll - showAll);
        string text = $"# 特效节奏预算与光敏（{battles} 场电脑代打，技能特写每次完整、代打也放完整版）\n\n" +
                      $"演出增量合计 {all * 100:0.0}%（目标 ≤ 15%）；单场最高 {worstRatio * 100:0.0}%（{worstBattle}）\n\n" +
                      $"全屏每秒闪白最多 {worstFlashes} 次（目标 ≤ 3）；超过 3 次的 1 秒窗口 {overWindows} 个；计入的闪白共 {flashesAll} 次\n\n" +
                      "| 战斗 | 演出增量 | 每秒闪白最多 |\n| --- | --- | --- |\n" + string.Join("\n", rows) + "\n";
        System.IO.File.WriteAllText(report, text);
        Debug.Log("HITFEEL_BUDGET\n" + text);
        Assert.GreaterOrEqual(battles, seeds >= 9 ? 100 : 12);
    }

    static string Shares(Dictionary<HitTier, int> t)
    {
        int n = Mathf.Max(1, t.Values.Sum());
        return string.Join("，", System.Enum.GetValues(typeof(HitTier)).Cast<HitTier>()
            .Select(k => $"{HitFeel.Name(k)} {(t.TryGetValue(k, out int c) ? c : 0) * 100f / n:0.0}%（{(t.TryGetValue(k, out int d) ? d : 0)}）"));
    }
}
