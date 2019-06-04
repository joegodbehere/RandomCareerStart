using BattleTech;
using Harmony;
using RandomCampaignStart.Features;

// ReSharper disable UnusedMember.Global
// ReSharper disable InconsistentNaming

namespace RandomCampaignStart.Patches
{
    [HarmonyPatch(typeof(SimGameState), "FirstTimeInitializeDataFromDefs")]
    public static class SimGameState_FirstTimeInitializeDataFromDefs_Patch
    {
        public static void Postfix(SimGameState __instance)
        {
            switch (__instance.SimGameMode)
            {
                case SimGameState.SimGameType.KAMEA_CAMPAIGN when Main.Settings.RandomizeStoryCampaign:
                case SimGameState.SimGameType.CAREER when __instance.Constants.CareerMode.StartWithRandomMechs:
                    RandomizeSimGame.Randomize(__instance);
                    break;
            }
        }
    }
}
