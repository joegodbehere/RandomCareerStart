using System.Reflection;
using Harmony;
using HBS.Logging;

// ReSharper disable UnusedMember.Global

namespace RandomCareerStart
{
    public static class Main
    {
        internal static ILog HBSLog;
        internal static ModSettings Settings;

        // ENTRY POINT
        public static void Init(string modDir, string modSettings)
        {
            var harmony = HarmonyInstance.Create("io.github.joegodbehere.RandomCareerStart");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            HBSLog = Logger.GetLogger("RandomCareerStart");
            Settings = ModSettings.ReadSettings(modSettings);
        }
    }
}
