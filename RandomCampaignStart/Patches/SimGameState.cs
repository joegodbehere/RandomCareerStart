using BattleTech;
using Harmony;

namespace RandomCampaignStart
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