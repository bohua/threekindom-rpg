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
    }
}
