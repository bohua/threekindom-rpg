using System.IO;
using ThreeKingdoms.Core.Fate;
using ThreeKingdoms.Data.Fate;
using UnityEditor;
using UnityEngine;

namespace ThreeKingdoms.EditorTools.Fate
{
    /// <summary>Menu entries for the fate validator (V-01…V-08) and the simulator report (spec §8).</summary>
    public static class FateTools
    {
        public const string TestDataDir = "Assets/_Project/Data/Fate/test";
        const string ReportDir = "Temp/FateReports";

        [MenuItem("Tools/Fate/Validate Test Data")]
        public static void ValidateTestData()
        {
            var issues = FateValidator.ValidateAll(FateJsonLoader.LoadDirectory(TestDataDir));
            if (issues.Count == 0) Debug.Log("[Fate] validation passed: V-01…V-06 (static), V-08");
            foreach (var i in issues) Debug.LogError("[Fate] " + i);
        }

        [MenuItem("Tools/Fate/Simulate Test Data (1,000 runs)")]
        public static void SimulateTestData()
        {
            var report = FateSimulator.Run(FateJsonLoader.LoadDirectory(TestDataDir), "tv", 1000);
            Directory.CreateDirectory(ReportDir);
            var path = Path.Combine(ReportDir, "fate-sim-tv.md");
            File.WriteAllText(path, report.ToText());
            Debug.Log("[Fate] simulator report written to " + path + "\n" + report.ToText());
        }

        /// <summary>Batch-mode entry: -executeMethod ThreeKingdoms.EditorTools.Fate.FateTools.BatchCheck (exit 1 on any issue).</summary>
        public static void BatchCheck()
        {
            var data = FateJsonLoader.LoadDirectory(TestDataDir);
            var issues = FateValidator.ValidateAll(data);
            foreach (var i in issues) Debug.LogError("[Fate] " + i);
            var report = FateSimulator.Run(data, "tv", 1000);
            Directory.CreateDirectory(ReportDir);
            File.WriteAllText(Path.Combine(ReportDir, "fate-sim-tv.md"), report.ToText());
            bool ok = issues.Count == 0 && report.TotalAnchorViolations == 0 && report.ReachabilityGaps.Count == 0;
            EditorApplication.Exit(ok ? 0 : 1);
        }
    }

    /// <summary>
    /// Minimal fate panel (S10): runs one settle on the test data and lists each character's candidates,
    /// scores, weights, cause tokens and arbitration; exports the result as JSON.
    /// </summary>
    public sealed class FatePanel : EditorWindow
    {
        SettleResult result;
        string node = "tv.N2";
        bool maxInteraction = true;
        ulong seed = 0x9E3779B97F4A7C15UL;
        Vector2 scroll;

        [MenuItem("Tools/Fate/Fate Panel")]
        public static void Open() => GetWindow<FatePanel>("命运面板");

        void OnGUI()
        {
            EditorGUILayout.LabelField("合成测试数据（tv）上的一次结算", EditorStyles.boldLabel);
            node = EditorGUILayout.TextField("进入节点", node);
            maxInteraction = EditorGUILayout.Toggle("最大交往（否则全不交互）", maxInteraction);
            var seedText = EditorGUILayout.TextField("fateSeed", FateStreams.FormatSeed(seed));
            try { seed = FateStreams.ParseSeed(seedText); } catch (System.FormatException) { }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("结算")) result = Run();
                using (new EditorGUI.DisabledScope(result == null))
                    if (GUILayout.Button("导出 JSON")) Export();
            }
            if (result == null) return;
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var e in result.Errors) EditorGUILayout.HelpBox(e, MessageType.Error);
            foreach (var a in result.Announced) EditorGUILayout.HelpBox($"宣告：{a.CharacterId} → {a.OptionId}（{a.CommitStage} 提交，预警 {a.WarningSceneId}）", MessageType.Info);
            foreach (var t in result.Trace)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField($"{t.CharacterId} [{t.Tier}] → {t.Chosen}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"因：{t.Cause}  令牌：{string.Join(", ", t.Tokens)}  仲裁：{(t.Arbitrated ? t.Note : "否")}");
                foreach (var c in t.Candidates)
                    EditorGUILayout.LabelField($"   {(c.IsDefault ? "★" : " ")} {c.OptionId}", $"{c.Status}  分 {c.Score}  权 {c.Weight}  {c.Cause}");
            }
            EditorGUILayout.EndScrollView();
        }

        SettleResult Run()
        {
            var data = FateJsonLoader.LoadDirectory(FateTools.TestDataDir);
            var policy = maxInteraction ? SimPolicy.MaxInteraction : SimPolicy.NoInteraction;
            var ctx = new SimConditionContext(policy);
            var s = FateEngine.CreateInitialState(data, "tv", seed);
            SettleResult r = null;
            for (string n = data.NextNode(s.world.stageId); n != null; n = data.NextNode(n))
            {
                var log = new StageEventLog();
                if (maxInteraction)
                    foreach (var c in s.characters.Values)
                        if (c.id != s.world.protagonistId && c.IsAlive)
                            log.Add(StageEvent.MemoryEvent(log.NextSeq(s), new MemoryEntry
                            {
                                who = c.id, key = "panel." + s.world.stageId, value = "max", kind = MemoryKinds.Player,
                                tags = data.Profile(c.id)?.values?.likes ?? new[] { "courtesy" }, intensity = 3, valence = 2, importance = 3
                            }));
                r = FateEngine.Settle(data, s, log, n, ctx);
                s = r.NewFateSection;
                if (n == node) break;
            }
            return r;
        }

        void Export()
        {
            var path = EditorUtility.SaveFilePanel("导出结算结果", "", "fate-settle-" + node + ".json", "json");
            if (string.IsNullOrEmpty(path)) return;
            File.WriteAllText(path, FateJsonLoader.ToJson(new { result.NewFateSection, result.Changes, result.Announced, result.Errors, result.Trace }));
        }
    }
}
