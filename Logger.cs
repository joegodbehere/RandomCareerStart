﻿
namespace RandomCareerStart
{
    /// <summary>
    /// Convenience class, it just filters out low level logs unless the settings are enabled.
    /// Also prefixes the messages with the mod name, to make them easier to spot.
    /// </summary>
    class Logger
    {
        public static string Prefix = "";

        public static void LogVerbose(string message)
        {
            if (Main.Settings.Debug && Main.Settings.DebugVerbose)
                Main.HBSLog.Log($"{Prefix}{message}");
        }

        public static void Log(string message)
        {
            if (Main.Settings.Debug)
                Main.HBSLog.Log($"{Prefix}{message}");
        }

        public static void LogWarning(string message)
        {
            Main.HBSLog.LogWarning($"{Prefix}{message}");
        }

        public static void LogError(string message)
        {
            Main.HBSLog.LogError($"{Prefix}{message}");
        }
    }
}
