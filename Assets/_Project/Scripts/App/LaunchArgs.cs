using System;

namespace ThreeKingdoms.App
{
    static class LaunchArgs
    {
        public static bool Has(string name) => Array.IndexOf(Environment.GetCommandLineArgs(), name) >= 0;

        public static string Get(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        // Probes start in the first build scene (the title); wait until a -scene switch has actually loaded.
        public static System.Collections.IEnumerator WaitForRequestedScene()
        {
            string scene = Get("-scene");
            float deadline = UnityEngine.Time.realtimeSinceStartup + 20f;
            while (scene != null && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != scene && UnityEngine.Time.realtimeSinceStartup < deadline)
                yield return null;
        }
    }
}
