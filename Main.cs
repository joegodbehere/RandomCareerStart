using System.Reflection;
using Harmony;

// ReSharper disable UnusedMember.Global

namespace RandomCareerStart
{
    public static class Main
    {
        internal static HBS.Logging.ILog HBSLog;
        internal static ModSettings Settings;

        // ENTRY POINT
        public static void Init(string modDir, string modSettings)
        {
            var harmony = HarmonyInstance.Create("com.battletech.RandomCareerStart");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            HBSLog = HBS.Logging.Logger.GetLogger("RandomCareerStart");
            Logger.Prefix = "RandomCareerStart: ";
            Settings = ModSettings.ReadSettings(modSettings);
        }
    }
}
