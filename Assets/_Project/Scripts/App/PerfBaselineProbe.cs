using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Profiling;

namespace ThreeKingdoms.App
{
    // Opt-in frame-time/memory sampler for baseline measurements (ADR-0001 step 5).
    // Process-level memory is measured from outside: System.Diagnostics.Process is unsupported under IL2CPP.
    // Inactive unless the player is started with: -perfBaseline <seconds> -perfOut <file> [-perfUncapped]
    public sealed class PerfBaselineProbe : MonoBehaviour
    {
        const float WarmupSeconds = 5f;
        float sampleSeconds;
        string outPath;
        float started;
        readonly List<float> frames = new List<float>(8192);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            string[] args = Environment.GetCommandLineArgs();
            string seconds = Arg(args, "-perfBaseline");
            if (seconds == null) return;
            Application.runInBackground = true; // keep sampling when the window doesn't get focus
            if (Array.IndexOf(args, "-perfUncapped") >= 0) { QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1; }
            var probe = new GameObject(nameof(PerfBaselineProbe)).AddComponent<PerfBaselineProbe>();
            probe.sampleSeconds = float.Parse(seconds, System.Globalization.CultureInfo.InvariantCulture);
            probe.outPath = Arg(args, "-perfOut") ?? Path.Combine(Application.persistentDataPath, "perf-baseline.json");
            DontDestroyOnLoad(probe.gameObject);
        }

        static string Arg(string[] args, string name)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        void Start() => started = Time.realtimeSinceStartup;

        void Update()
        {
            float elapsed = Time.realtimeSinceStartup - started;
            if (elapsed < WarmupSeconds) return;
            frames.Add(Time.unscaledDeltaTime * 1000f);
            if (elapsed < WarmupSeconds + sampleSeconds) return;
            enabled = false;
            try { Write(); }
            catch (Exception e) { Debug.LogException(e); }
            Application.Quit();
        }

        void Write()
        {
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            var sorted = new List<float>(frames); sorted.Sort();
            double sum = 0; foreach (float f in frames) sum += f;
            float P(float q) => sorted[Math.Min(sorted.Count - 1, (int)(q * sorted.Count))];
            string json = "{\n" +
                $"  \"frames\": {frames.Count},\n" +
                $"  \"avgMs\": {sum / frames.Count:F3},\n" +
                $"  \"p50Ms\": {P(.5f):F3},\n  \"p95Ms\": {P(.95f):F3},\n  \"p99Ms\": {P(.99f):F3},\n  \"maxMs\": {sorted[sorted.Count - 1]:F3},\n" +
                $"  \"vSyncCount\": {QualitySettings.vSyncCount},\n" +
                $"  \"resolution\": \"{Screen.width}x{Screen.height}\",\n" +
                $"  \"monoUsedMB\": {Profiler.GetMonoUsedSizeLong() / 1048576.0:F1},\n" +
                $"  \"totalAllocatedMB\": {Profiler.GetTotalAllocatedMemoryLong() / 1048576.0:F1},\n" +
                $"  \"totalReservedMB\": {Profiler.GetTotalReservedMemoryLong() / 1048576.0:F1},\n" +
                $"  \"graphicsDriverMB\": {Profiler.GetAllocatedMemoryForGraphicsDriver() / 1048576.0:F1},\n" +
                $"  \"gpu\": \"{SystemInfo.graphicsDeviceName}\",\n" +
                $"  \"cpu\": \"{SystemInfo.processorType}\"\n}}\n";
            File.WriteAllText(outPath, json);
        }
    }
}
