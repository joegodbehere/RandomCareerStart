using BattleTech;
using Harmony;

// ReSharper disable UnusedMember.Global
// ReSharper disable InconsistentNaming

namespace RandomCampaignStart.Patches
{
    [HarmonyPatch(typeof(SimGameState), "FirstTimeInitializeDataFromDefs")]
    public static class SimGameState_FirstTimeInitializeDataFromDefs_Patch
    {
        public static void Postfix(SimGameState __instance)
        {
            Main.RandomizeSimGame(__instance);
        }
    }
}
