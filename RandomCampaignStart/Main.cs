using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BattleTech;
using Harmony;
using HBS.Logging;

namespace RandomCampaignStart
{
    public static class Main
    {
        internal static ILog HBSLog;
        internal static ModSettings Settings;


        // ENTRY POINT
        public static void Init(string modDir, string modSettings)
        {
            var harmony = HarmonyInstance.Create("io.github.mpstark.RandomCampaignStart");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            HBSLog = Logger.GetLogger("RandomCampaignStart");
            Settings = ModSettings.ReadSettings(modSettings);
        }


        // UTIL
        private static readonly Random rng = new Random();
        public static void RNGShuffle<T>(this IList<T> list)
        {
            // from https://stackoverflow.com/questions/273313/randomize-a-listt
            var n = list.Count;
            while (n > 1)
            {
                n--;
                var k = rng.Next(n + 1);
                var value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        public static List<T> GetRandomSubList<T>(List<T> list, int numEntries)
        {
            var subList = new List<T>();

            if (list.Count <= 0 || numEntries <= 0)
                return subList;

            var randomizeMe = new List<T>(list);

            // add enough duplicates of the list to satisfy the number specified
            while (randomizeMe.Count < numEntries)
                randomizeMe.AddRange(list);

            randomizeMe.RNGShuffle();
            for (var i = 0; i < numEntries; i++)
                subList.Add(randomizeMe[i]);

            return subList;
        }

        public static void ReplacePilotStats(PilotDef pilotDef, PilotDef replacementStatPilotDef, SimGameState simGameState)
        {
            // set all stats to the subPilot stats
            pilotDef.AddBaseSkill(SkillType.Gunnery, replacementStatPilotDef.BaseGunnery - pilotDef.BaseGunnery);
            pilotDef.AddBaseSkill(SkillType.Piloting, replacementStatPilotDef.BasePiloting - pilotDef.BasePiloting);
            pilotDef.AddBaseSkill(SkillType.Guts, replacementStatPilotDef.BaseGuts - pilotDef.BaseGuts);
            pilotDef.AddBaseSkill(SkillType.Tactics, replacementStatPilotDef.BaseTactics - pilotDef.BaseTactics);

            pilotDef.ResetBonusStats();
            pilotDef.AddSkill(SkillType.Gunnery, replacementStatPilotDef.BonusGunnery);
            pilotDef.AddSkill(SkillType.Piloting, replacementStatPilotDef.BonusPiloting);
            pilotDef.AddSkill(SkillType.Guts, replacementStatPilotDef.BonusGuts);
            pilotDef.AddSkill(SkillType.Tactics, replacementStatPilotDef.BonusTactics);

            // set exp to replacementStatPilotDef
            pilotDef.SetSpentExperience(replacementStatPilotDef.ExperienceSpent);
            pilotDef.SetUnspentExperience(replacementStatPilotDef.ExperienceUnspent);

            // copy abilities
            pilotDef.abilityDefNames.Clear();
            pilotDef.abilityDefNames.AddRange(replacementStatPilotDef.abilityDefNames);
            if (pilotDef.AbilityDefs != null)
                pilotDef.AbilityDefs.Clear();
            pilotDef.ForceRefreshAbilityDefs();
        }


        // MEAT
        public static void RandomizeSimGame(SimGameState simGame)
        {
            if (Settings.StartingRonin.Count + Settings.NumberRandomRonin + Settings.NumberProceduralPilots > 0)
            {
                HBSLog.Log("Randomizing pilots, removing old pilots");

                // clear roster
                while (simGame.PilotRoster.Count > 0)
                    simGame.PilotRoster.RemoveAt(0);

                // starting ronin that are always present
                if (Settings.StartingRonin != null && Settings.StartingRonin.Count > 0)
                {
                    foreach (var pilotID in Settings.StartingRonin)
                    {
                        if (!simGame.DataManager.PilotDefs.Exists(pilotID))
                        {
                            HBSLog.LogWarning($"\tMISSING StartingRonin {pilotID}!");
                            continue;
                        }

                        var pilotDef = simGame.DataManager.PilotDefs.Get(pilotID);

                        if (Settings.RerollRoninStats)
                            ReplacePilotStats(pilotDef, simGame.PilotGenerator.GeneratePilots(1, Settings.PilotPlanetDifficulty, 0, out _)[0], simGame);

                        simGame.AddPilotToRoster(pilotDef, true);
                        HBSLog.Log($"\tAdding StartingRonin {pilotDef.Description.Id}");
                    }
                }

                // random ronin
                if (Settings.NumberRandomRonin > 0)
                {
                    // make sure to remove the starting ronin list from the possible random pilots! yay linq
                    var randomRonin = GetRandomSubList(simGame.RoninPilots.Where(x => !Settings.StartingRonin.Contains(x.Description.Id)).ToList(), Settings.NumberRandomRonin);
                    foreach (var pilotDef in randomRonin)
                    {
                        if (Settings.RerollRoninStats)
                            ReplacePilotStats(pilotDef, simGame.PilotGenerator.GeneratePilots(1, Settings.PilotPlanetDifficulty, 0, out _)[0], simGame);

                        simGame.AddPilotToRoster(pilotDef, true);
                        HBSLog.Log($"\tAdding random Ronin {pilotDef.Description.Id}");
                    }
                }

                // random prodedural pilots
                if (Settings.NumberProceduralPilots > 0)
                {
                    var randomProcedural = simGame.PilotGenerator.GeneratePilots(Settings.NumberProceduralPilots, Settings.PilotPlanetDifficulty, 0, out _);
                    foreach (var pilotDef in randomProcedural)
                    {
                        simGame.AddPilotToRoster(pilotDef, true);
                        HBSLog.Log($"\tAdding random procedural pilot {pilotDef.Description.Id}");
                    }
                }
            }

            // mechs
            if (Settings.NumberLightMechs + Settings.NumberMediumMechs + Settings.NumberHeavyMechs + Settings.NumberAssaultMechs > 0)
            {
                HBSLog.Log("Randomizing mechs, removing old mechs");

                // clear the initial lance
                for (var i = 1; i < simGame.Constants.Story.StartingLance.Length + 1; i++)
                    simGame.ActiveMechs.Remove(i);

                // remove ancestral mech if specified
                if (Settings.RemoveAncestralMech)
                {
                    HBSLog.Log("\tRemoving ancestral mech");
                    simGame.ActiveMechs.Remove(0);
                }

                // add the random mechs to mechIds
                var mechIds = new List<string>();
                mechIds.AddRange(GetRandomSubList(Settings.AssaultMechsPossible, Settings.NumberAssaultMechs));
                mechIds.AddRange(GetRandomSubList(Settings.HeavyMechsPossible, Settings.NumberHeavyMechs));
                mechIds.AddRange(GetRandomSubList(Settings.MediumMechsPossible, Settings.NumberMediumMechs));
                mechIds.AddRange(GetRandomSubList(Settings.LightMechsPossible, Settings.NumberLightMechs));

                // remove mechIDs that don't have a valid mechDef
                var numInvalid = mechIds.RemoveAll(id => !simGame.DataManager.MechDefs.Exists(id));
                if (numInvalid > 0)
                {
                    HBSLog.LogWarning($"\tREMOVED {numInvalid} INVALID MECHS! Check mod.json for misspellings");
                }

                // actually add the mechs to the game
                foreach (var mechID in mechIds)
                {
                    var baySlot = simGame.GetFirstFreeMechBay();
                    var mechDef = new MechDef(simGame.DataManager.MechDefs.Get(mechID), simGame.GenerateSimGameUID());

                    if (baySlot >= 0)
                    {
                        HBSLog.Log($"\tAdding {mechID} to bay {baySlot}");
                        simGame.AddMech(baySlot, mechDef, true, true, false);
                    }
                    else
                    {
                        HBSLog.Log($"\tAdding {mechID} to storage, bays are full");
                        simGame.AddItemStat(mechDef.Chassis.Description.Id, mechDef.GetType(), false);
                    }
                }
            }
        }
    }
}
