using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace RandomCareerStart
{
    internal class ModSettings
    {
        // pilots
        public bool RandomizePilots = true;
        public List<string> StartingRonin = new List<string>();
        public int NumberRoninFromList = 0;
        public int NumberRandomRonin = 0;
        public int NumberProceduralPilots = 3;
        public int PilotPlanetDifficulty = 1;

        // mechs
        public bool RandomizeMechs = true;

        // manual filters
        public bool UseWhitelist = false;
        public List<string> Whitelist = new List<string>();
        public List<string> Blacklist = new List<string>();

        //public int NumberAssaultMechs = 0;
        //public int NumberHeavyMechs = 0;
        //public int NumberLightMechs = 3;
        //public int NumberMediumMechs = 1;
        //public bool RemoveAncestralMech = false;

        public bool MechsAdhereToTimeline = true;

        public float MechPercentageStartingCost = 0;

        // lance restrictions
        public int MinimumLanceSize = 5;
        public int MaximumLanceSize = 6;
        public float MinimumLanceTonnage = 145;
        public float MaximumLanceTonnage = 150;

        // mech restrictions
        public float MinimumMechTonnage = 20;
        public float MaximumMechTonnage = 45;
        public int MaximumRVMechs = 1;
        public int MaximumGhettoMechs = 1;
        public int MinimumMediumMechs = 1;
        public int MaximumMediumMechs = 1;
        public bool AllowDuplicateChassis = false;
        public bool AllowDuplicateMech = false;
        public bool RandomiseAncestralVariant = false;

        public bool Infestation = false;
        public bool Debug = false;
        public bool DebugVerbose = false;

        public static ModSettings ReadSettings(string json)
        {
            ModSettings settings;

            try
            {
                settings = JsonConvert.DeserializeObject<ModSettings>(json);
            }
            catch (Exception e)
            {
                Main.HBSLog.Log($"Reading settings failed: {e.Message}");
                settings = new ModSettings();
            }

            return settings;
        }
    }
}
