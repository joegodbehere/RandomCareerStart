using BattleTech;
using Harmony;
using RandomCareerStart.Features;

// ReSharper disable UnusedMember.Global
// ReSharper disable InconsistentNaming

namespace RandomCareerStart.Patches
{
    [HarmonyPatch(typeof(SimGameState), "FirstTimeInitializeDataFromDefs")]
    public static class SimGameState_FirstTimeInitializeDataFromDefs_Patch
    {
        public static void Postfix(SimGameState __instance)
        {
            RandomizeCareerPilots.TryRandomize(__instance);
            RandomizeCareerMechs.TryRandomize(__instance);
        }
    }

    // prevent the vanilla lance from spawning
    [HarmonyPatch(typeof(SimGameState), "AddCareerMechs")]
    public static class SimGameState_AddCareerMechs_Patch
    {
        public static bool Prefix()
        {
            return false;
        }
    }

    [HarmonyPatch(typeof(SimGameState), "_OnDefsLoadComplete")]
    public static class Initialize_New_Game
    {
        public static void Postfix(SimGameState __instance)
        {
            if (Main.Settings.MechPercentageStartingCost <= 0)
                return;

            float cost = 0;
            foreach (MechDef mech in __instance.ActiveMechs.Values)
            {
                cost += mech.Description.Cost * (0.01f * Main.Settings.MechPercentageStartingCost);
            }
            __instance.AddFunds(-(int)cost, null, false);
        }
    }


}
