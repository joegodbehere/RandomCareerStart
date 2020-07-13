using System.Collections.Generic;
using System.Linq;
using BattleTech;
using Harmony;

namespace RandomCareerStart.Features
{
    public class RandomizeCareerMechs
    {
        private List<MechDef> PossibleMechs = new List<MechDef>();
        //private Dictionary<float, List<MechDef>> PossibleByTonnage = new Dictionary<float, List<MechDef>>();
        private List<MechDef> PossibleAssault = new List<MechDef>();
        private List<MechDef> PossibleHeavy = new List<MechDef>();
        private List<MechDef> PossibleMedium = new List<MechDef>();
        private List<MechDef> PossibleLight = new List<MechDef>();


        public static void TryRandomize(SimGameState simGame)
        {
            if (simGame.SimGameMode != SimGameState.SimGameType.CAREER)
                return;

            if (!Main.Settings.RandomizeMechs)
                return;

            if (Main.Settings.Infestation && simGame.Constants.CareerMode.StartingPlayerMech.Contains("FLE"))
            {
                Infestation(simGame);
                return;
            }

            //TODO sanity check the inputs
            // Main.HBSLog.LogWarning("Tried to randomize mechs but settings had 0!");

            int numLances = 10;
            var lances = new List<List<MechDef>>();

            var randy = new RandomizeCareerMechs();
            for (int i = 0; i < numLances; ++i)
            {
                randy.FilterPossibleMechs(simGame);
                List<MechDef> lance = randy.GetRandomLance(simGame);
                lances.Add(lance);
            }
            lances = lances.OrderBy(lance => lance.Sum(mech => mech.Chassis.Tonnage)).ToList();
            Main.HBSLog.Log($"Generated potential starting lances:");
            foreach (var lance in lances)
            {
                float lanceTonnage = lance.Sum(mech => mech.Chassis.Tonnage);
                Main.HBSLog.Log($"\tLance size {lance.Count} with tonnage {lanceTonnage}: {string.Join(", ", lance.Select(mech => mech.ChassisID))}");
            }
            // pick out the lances that didn't blow the tonnage budget
            var optimalLances = lances.FindAll(lance => lance.Sum(mech => mech.Chassis.Tonnage) >= Main.Settings.MinimumLanceTonnage
                    && lance.Sum(mech => mech.Chassis.Tonnage) >= Main.Settings.MinimumLanceTonnage);
            if (optimalLances.Count > 0)
            {
                ApplyLance(simGame, optimalLances[optimalLances.GetRandomIndex()]);
            }
            // if there weren't any (very unlikely), just take the lightest lance
            else
            {
                ApplyLance(simGame, lances[0]);
            }
        }


        public static void Infestation(SimGameState simGame)
        {
            // Open bays 0-11, the extra 2 drop spots, and command consoles to match (assumes BT:R)
            simGame.AddArgoUpgrade(simGame.DataManager.ShipUpgradeDefs.Get("argoUpgrade_structure1"));
            simGame.AddArgoUpgrade(simGame.DataManager.ShipUpgradeDefs.Get("argoUpgrade_mechBay2"));
            simGame.AddArgoUpgrade(simGame.DataManager.ShipUpgradeDefs.Get("argoUpgrade_JunkyardLeopard"));
            simGame.AddArgoUpgrade(simGame.DataManager.ShipUpgradeDefs.Get("argoUpgrade_DropSlot"));
            simGame.AddArgoUpgrade(simGame.DataManager.ShipUpgradeDefs.Get("argoUpgrade_DropSlot1"));
            simGame.AddArgoUpgrade(simGame.DataManager.ShipUpgradeDefs.Get("argoUpgrade_PilotSlot"));
            simGame.AddArgoUpgrade(simGame.DataManager.ShipUpgradeDefs.Get("argoUpgrade_PilotSlot1"));
            simGame.ApplyArgoUpgrades();
            List<MechDef> lance = new List<MechDef>();
            var fle4 = simGame.DataManager.MechDefs.Get("mechdef_flea_FLE-4");
            var fle15 = simGame.DataManager.MechDefs.Get("mechdef_flea_FLE-15");
            var fle4rv = simGame.DataManager.MechDefs.Get("mechdef_flea_FLE-4-RV");
            var fle15rv = simGame.DataManager.MechDefs.Get("mechdef_flea_FLE-15-RV");
            lance.Add(fle4);
            lance.Add(fle4);
            lance.Add(fle4);
            lance.Add(fle4);
            lance.Add(fle4rv);
            lance.Add(fle4rv);
            lance.Add(fle15);
            lance.Add(fle15);
            lance.Add(fle15);
            lance.Add(fle15);
            lance.Add(fle15rv);
            lance.Add(fle15rv);
            //ApplyLance(simGame, lance);
            // For some reason, bays 6-11 still aren't marked as available at this point
            // ... but manually forcing the mechs into those bays seems to work fine.
            for (int i = 0; i < lance.Count; ++i)
            {
                var mech = new MechDef(lance[i], simGame.GenerateSimGameUID());
                Main.HBSLog.Log($"\tAdding {mech.ChassisID} to bay {i}");
                simGame.AddMech(i, mech, true, true, false);
            }
        }


        public void FilterPossibleMechs(SimGameState simGame)
        {
            if (Main.Settings.UseWhitelist)
            {
                PossibleMechs = new List<MechDef>();

                // remove items on whitelist that aren't in the datamanager
                if (Main.Settings.Debug)
                    Main.Settings.Whitelist.FindAll(id => !simGame.DataManager.MechDefs.Exists(id))
                            .Do(id => Main.HBSLog.LogWarning($"\tInvalid MechDef '{id}'. Will remove from possibilities"));
                Main.Settings.Whitelist.RemoveAll(id => !simGame.DataManager.MechDefs.Exists(id));
                PossibleMechs = Main.Settings.Whitelist.Select(id => simGame.DataManager.MechDefs.Get(id)).ToList();
            }
            else
            {
                // extract the mechdefs 
                var mechKeys = simGame.DataManager.MechDefs.Keys.ToList();
                Main.HBSLog.Log($"\tPossible mech count initial: {mechKeys.Count}");

                // remove mechs from blacklist in settings
                if (Main.Settings.Debug)
                    mechKeys.FindAll(id => Main.Settings.Blacklist.Contains(id))
                            .Do(id => Main.HBSLog.Log($"\tRemoving blacklisted (by settings) MechDef '{id}' from possibilities"));
                mechKeys.RemoveAll(id => Main.Settings.Blacklist.Contains(id));
                Main.HBSLog.Log($"\tPossible mech count after blacklist (settings): {mechKeys.Count}");

                // remove mechs with undesirable labels (i.e. from the Skirmish MechBay
                if (Main.Settings.Debug)
                    mechKeys.FindAll(id => id.Contains("CUSTOM"))
                            .Do(id => Main.HBSLog.Log($"\tRemoving CUSTOM MechDef '{id}' from possibilities"));
                mechKeys.RemoveAll(id => id.Contains("CUSTOM"));
                Main.HBSLog.Log($"\tPossible mech count after CUSTOM (name): {mechKeys.Count}");

                if (Main.Settings.Debug)
                    mechKeys.FindAll(id => id.Contains("DUMMY"))
                            .Do(id => Main.HBSLog.Log($"\tRemoving DUMMY MechDef '{id}' from possibilities"));
                mechKeys.RemoveAll(id => id.Contains("DUMMY"));
                Main.HBSLog.Log($"\tPossible mech count after DUMMY (name): {mechKeys.Count}");

                if (Main.Settings.Debug)
                    mechKeys.FindAll(id => id.Contains("DUMMY"))
                            .Do(id => Main.HBSLog.Log($"\tRemoving DUMMY MechDef '{id}' from possibilities"));
                mechKeys.RemoveAll(id => id.Contains("DUMMY"));
                Main.HBSLog.Log($"\tPossible mech count after DUMMY (name): {mechKeys.Count}");

                // convert keys to mechdefs
                PossibleMechs = mechKeys.Select(id => simGame.DataManager.MechDefs.Get(id)).ToList();

                // remove mechs with undesirable tags
                if (Main.Settings.Debug)
                    PossibleMechs.FindAll(mech => mech.MechTags.Contains("BLACKLISTED"))
                            .Do(mech => Main.HBSLog.Log($"\tRemoving blacklisted (by tag) MechDef '{mech.ChassisID}' from possibilities"));
                PossibleMechs.RemoveAll(mech => mech.MechTags.Contains("BLACKLISTED"));
                Main.HBSLog.Log($"\tPossible mech count after BLACKLISTED (tag): {PossibleMechs.Count}");

                // remove mechs outside weight restrictions
                if (Main.Settings.Debug)
                    PossibleMechs.FindAll(mech => mech.Chassis.Tonnage < Main.Settings.MinimumMechTonnage)
                            .Do(mech => Main.HBSLog.Log($"\tRemoving underweight MechDef '{mech.ChassisID}' from possibilities"));
                PossibleMechs.RemoveAll(mech => mech.Chassis.Tonnage < Main.Settings.MinimumMechTonnage);
                Main.HBSLog.Log($"\tPossible mech count after removing underweight: {PossibleMechs.Count}");

                if (Main.Settings.Debug)
                    PossibleMechs.FindAll(mech => mech.Chassis.Tonnage > Main.Settings.MaximumMechTonnage)
                            .Do(mech => Main.HBSLog.Log($"\tRemoving overweight MechDef '{mech.ChassisID}' from possibilities"));
                PossibleMechs.RemoveAll(mech => mech.Chassis.Tonnage > Main.Settings.MaximumMechTonnage);
                Main.HBSLog.Log($"\tPossible mech count after removing overweight: {PossibleMechs.Count}");

                // remove mechs that don't exist yet
                if (Main.Settings.MechsAdhereToTimeline)
                {
                    var startDate = simGame.GetCampaignStartDate();
                    if (Main.Settings.Debug)
                    {
                        PossibleMechs.FindAll(mech => mech.MinAppearanceDate.HasValue && mech.MinAppearanceDate > startDate)
                                .Do(mech => Main.HBSLog.Log($"\tRemoving anachronistic MechDef '{mech.ChassisID}' from possibilities"));
                    }
                    PossibleMechs.RemoveAll(mech => mech.MinAppearanceDate.HasValue && mech.MinAppearanceDate > startDate);
                    Main.HBSLog.Log($"\tPossible mech count after enforcing timeline: {PossibleMechs.Count}");
                }
            }

            // remove mechs with broken names (weird, but seen it once before)
            if (Main.Settings.Debug)
                PossibleMechs.FindAll(mech => mech.Description.UIName == null)
                    .Do(mech => Main.HBSLog.Log($"\tRemoving MechDef with null UIName '{mech.ChassisID}' from possibilities"));
            PossibleMechs.RemoveAll(mech => mech.Description.UIName == null);
            Main.HBSLog.Log($"\tPossible mech count after removing null UIName: {PossibleMechs.Count}");

            // sort possible mechs into buckets
            PossibleAssault = new List<MechDef>(PossibleMechs.FindAll(mech => mech.Chassis.weightClass == WeightClass.ASSAULT));
            PossibleHeavy = new List<MechDef>(PossibleMechs.FindAll(mech => mech.Chassis.weightClass == WeightClass.HEAVY));
            PossibleMedium = new List<MechDef>(PossibleMechs.FindAll(mech => mech.Chassis.weightClass == WeightClass.MEDIUM));
            PossibleLight = new List<MechDef>(PossibleMechs.FindAll(mech => mech.Chassis.weightClass == WeightClass.LIGHT));

            // finer grained buckets (unused):
            //PossibleByTonnage = new Dictionary<float, List<MechDef>>();
            //foreach (var mech in PossibleMechs)
            //{
            //    float tonnage = mech.Chassis.Tonnage;
            //    if (!PossibleByTonnage.ContainsKey(tonnage))
            //        PossibleByTonnage.Add(tonnage, new List<MechDef>());
            //    PossibleByTonnage[tonnage].Add(mech);
            //}
        }


        private void RemoveMech(MechDef mech)
        {
            //PossibleByTonnage[mech.Chassis.Tonnage].Remove(mech);
            PossibleAssault.Remove(mech);
            PossibleHeavy.Remove(mech);
            PossibleMedium.Remove(mech);
            PossibleLight.Remove(mech);
            PossibleMechs.Remove(mech);
        }


        private void RemoveChassis(ChassisDef chassis)
        {
            string prefix = chassis.Description.Id.Substring(0, 12);
            //PossibleByTonnage[mech.Chassis.Tonnage].RemoveAll(mech => mech.Chassis.Description.Id.Substring(0, 12).Equals(prefix));
            PossibleAssault.RemoveAll(mech => mech.Chassis.Description.Id.Substring(0, 12).Equals(prefix));
            PossibleHeavy.RemoveAll(mech => mech.Chassis.Description.Id.Substring(0, 12).Equals(prefix));
            PossibleMedium.RemoveAll(mech => mech.Chassis.Description.Id.Substring(0, 12).Equals(prefix));
            PossibleLight.RemoveAll(mech => mech.Chassis.Description.Id.Substring(0, 12).Equals(prefix));
            PossibleMechs.RemoveAll(mech => mech.Chassis.Description.Id.Substring(0, 12).Equals(prefix));
        }


        private bool SelectRandomMech(List<MechDef> mechs, out MechDef mech)
        {
            if (mechs.Count > 0)
            {
                mech = mechs[mechs.GetRandomIndex()];
                if (Main.Settings.AllowDuplicateChassis == false)
                    RemoveMech(mech);
                return true;
            }
            mech = null;
            return false;
        }

        
        // unused
        //private bool SelectRandomMech(float tonnage, out MechDef mech)
        //{
        //    if (PossibleByTonnage.ContainsKey(tonnage))
        //    {
        //        return SelectRandomMech(PossibleByTonnage[tonnage], out mech);
        //    }
        //    Main.HBSLog.Log($"\tCould not find another mech with tonnage {tonnage}");
        //    mech = null;
        //    return false;
        //}


        private bool SelectRandomMech(WeightClass weightClass, out MechDef mech)
        {
            List<MechDef> mechs;
            switch (weightClass)
            {
                case WeightClass.LIGHT: mechs = PossibleLight; break;
                case WeightClass.MEDIUM: mechs = PossibleMedium; break;
                case WeightClass.HEAVY: mechs = PossibleHeavy; break;
                default: mechs = PossibleAssault; break;
            }
            return SelectRandomMech(mechs, out mech);
        }


        private List<MechDef> GetRandomLance(SimGameState simGame)
        {
            Main.HBSLog.Log("Randomizing mechs, removing old mechs");

            List<MechDef> lance = new List<MechDef>();
            float lanceTonnage = 0;

            // see if the player (in BTR) selected an ancestral mech
            string ancestralMechKey = simGame.Constants.CareerMode.StartingPlayerMech;
            if (ancestralMechKey != "mechdef_centurion_TARGETDUMMY")
            {
                if (!simGame.DataManager.MechDefs.Keys.Contains(ancestralMechKey))
                {
                    Main.HBSLog.LogError($"\tSelected career ancestral mech {ancestralMechKey} was not found in Datastore. Skipping.");
                }
                MechDef mech = simGame.DataManager.MechDefs.Get(ancestralMechKey);
                lance.Add(mech);
                lanceTonnage += mech.Chassis.Tonnage;
                Main.HBSLog.Log($"\tAdded ancestral mech: {mech.ChassisID}");
                if (Main.Settings.AllowDuplicateMech == false)
                {
                    RemoveMech(mech);
                }
                if (Main.Settings.AllowDuplicateChassis == false)
                {
                    RemoveChassis(mech.Chassis);
                }
            }
            else
            {
                Main.HBSLog.Log($"\tNo ancestral mech specified (i.e. random)");
            }

            while (lance.FindAll(mech => mech.Chassis.weightClass == WeightClass.MEDIUM).Count() < Main.Settings.MinimumMediumMechs)
            {
                Main.HBSLog.Log($"\tPicking a medium mech...");
                if (SelectRandomMech(WeightClass.MEDIUM, out MechDef mech))
                {
                    lance.Add(mech);
                    lanceTonnage += mech.Chassis.Tonnage;
                    Main.HBSLog.Log($"\tSelected {mech.ChassisID} with tonnage {mech.Chassis.Tonnage}, lance is now {lanceTonnage} tons");
                }
                else
                {
                    Main.HBSLog.Log($"\tFailed to add the required number of medium mechs");
                    break;
                }
            }

            // now fill out the remainder of the lance
            PossibleMechs = PossibleMechs.OrderBy(mech => mech.Chassis.Tonnage).ToList();
            bool filteredRV = false;
            bool filteredMedium = false;
            bool filteredGhetto = false;
            while (lance.Count < Main.Settings.MinimumLanceSize
                    || (lance.Count < Main.Settings.MaximumLanceSize && lanceTonnage < Main.Settings.MinimumLanceTonnage))
            {
                // apply some dynamic filters:
                if (Main.Settings.Debug) Main.HBSLog.Log("\tchecking RV count");
                if (!filteredRV && Main.Settings.MaximumRVMechs <= lance.FindAll(mech => mech.Description.UIName.Contains("-RV")).Count)
                {
                    if (Main.Settings.Debug)
                        PossibleMechs.FindAll(mech => mech.Description.UIName.EndsWith("-RV"))
                                .Do(mech => Main.HBSLog.Log($"\tRemoving RV MechDef '{mech.ChassisID}' from possibilities"));
                    PossibleMechs.RemoveAll(mech => mech.Description.UIName.Contains("-RV"));
                    filteredRV = true;
                }
                if (Main.Settings.Debug) Main.HBSLog.Log("\tchecking medium count");
                if (!filteredMedium && Main.Settings.MaximumMediumMechs <= lance.FindAll(mech => mech.Chassis.weightClass == WeightClass.MEDIUM).Count)
                {
                    if (Main.Settings.Debug)
                    {
                        Main.HBSLog.Log($"\tMax mediums reached, filtering out remaining mediums");
                        PossibleMechs.FindAll(mech => mech.Chassis.weightClass == WeightClass.MEDIUM)
                                .Do(mech => Main.HBSLog.Log($"\tRemoving Medium MechDef '{mech.ChassisID}' from possibilities"));
                    }
                    PossibleMechs.RemoveAll(mech => mech.Chassis.weightClass == WeightClass.MEDIUM);
                    filteredMedium = true;
                }
                if (Main.Settings.Debug) Main.HBSLog.Log("\tchecking ghetto count");
                if (!filteredGhetto && Main.Settings.MaximumGhettoMechs <= lance.FindAll(mech => mech.Chassis.Tonnage <= 20).Count)
                {
                    if (Main.Settings.Debug)
                    {
                        Main.HBSLog.Log($"\tMax ghetto (20 tonners) reached, filtering out remaining 20 tonners");
                        PossibleMechs.FindAll(mech => mech.Chassis.Tonnage <= 20)
                                .Do(mech => Main.HBSLog.Log($"\tRemoving ghetto (<=20 ton) MechDef '{mech.ChassisID}' from possibilities"));
                    }
                    PossibleMechs.RemoveAll(mech => mech.Chassis.Tonnage <= 20);
                    filteredGhetto = true;
                }

                SelectSuitableMechForLance(simGame, lance, out MechDef candidate);
                lance.Add(candidate);
                lanceTonnage += candidate.Chassis.Tonnage;
                Main.HBSLog.Log($"\tSelected {candidate.ChassisID} with tonnage {candidate.ChassisID}, lance is now {lanceTonnage} tons");
                if (Main.Settings.AllowDuplicateChassis == false)
                    RemoveMech(candidate);
            }
            Main.HBSLog.Log($"\tSelected lance size {lance.Count} with tonnage {lanceTonnage}: {string.Join(", ", lance)}");
            return lance;
        }


        private bool SelectSuitableMechForLance(SimGameState simGame, List<MechDef> lance, out MechDef candidate)
        {
            if (Main.Settings.Debug)
            {
                Main.HBSLog.Log($"Entering SelectSuitableMechForLance");
            }
            // avoid risk of index out of bounds error
            if (PossibleMechs.Count > 0)
            {
                // follow two defs assume PossibleMechs is sorted by tonnage, and excludes invalid weight mechs
                float maxMechTonnage = PossibleMechs[PossibleMechs.Count - 1].Chassis.Tonnage;
                float minMechTonnage = PossibleMechs[0].Chassis.Tonnage;
                // how much does the current lance weigh?
                float lanceTonnage = lance.Sum(mech => mech.Chassis.Tonnage);
                // if the mech weighs more than this, min lance size & max lance tonnage cannot both be satisfied
                if (lance.Count < Main.Settings.MinimumLanceSize)
                {
                    // TODO: needs to consider MaximumGhettoMechs constraint
                    maxMechTonnage = (Main.Settings.MaximumLanceTonnage - lanceTonnage)
                            - (minMechTonnage * (Main.Settings.MinimumLanceSize - lance.Count - 1));
                }
                else
                {
                    maxMechTonnage = Main.Settings.MaximumLanceTonnage - lanceTonnage;
                }
                // if the mech weighs less than this, max lance size & min lace tonnage cannot both be satisfied
                if (lanceTonnage < Main.Settings.MinimumLanceTonnage)
                {
                    // TODO: needs to consider MaximumMediumMechs constraint
                    minMechTonnage = (Main.Settings.MinimumLanceTonnage - lanceTonnage)
                            - (maxMechTonnage * (Main.Settings.MaximumLanceSize - lance.Count - 1));
                }
                else
                {
                    minMechTonnage = 0;
                }
                Main.HBSLog.Log($"\tSelecting a mech with tonnage in the range [{minMechTonnage}, {maxMechTonnage}]");
                if (Main.Settings.Debug)
                {
                    PossibleMechs.FindAll(mech => mech.Chassis.Tonnage < minMechTonnage)
                            .Do(mech => Main.HBSLog.Log($"\tRemoving underweight MechDef '{mech.ChassisID}' from possibilities"));
                    PossibleMechs.FindAll(mech => mech.Chassis.Tonnage > maxMechTonnage)
                            .Do(mech => Main.HBSLog.Log($"\tRemoving overweight MechDef '{mech.ChassisID}' from possibilities"));
                }
                PossibleMechs.RemoveAll(mech => mech.Chassis.Tonnage < minMechTonnage);
                PossibleMechs.RemoveAll(mech => mech.Chassis.Tonnage > maxMechTonnage);
            }
            if (!SelectRandomMech(PossibleMechs, out candidate))
            {
                Main.HBSLog.LogError($"\tNo valid mechs left to choose from! It's spiders all the way down!");
                candidate = simGame.DataManager.MechDefs.Get("mechdef_spider_SDR-5V");
                return false;
            }
            return true;
        }


        private static void ApplyLance(SimGameState simGame, List<MechDef> lance)
        {
            // actually add the mechs to the game, in descending order of tonnage
            foreach (var mechDef in lance.OrderBy(mech => -mech.Chassis.Tonnage))
            {
                // pick a slot, and generate the mechdef a UID
                var baySlot = simGame.GetFirstFreeMechBay();
                var concreteMech = new MechDef(mechDef, simGame.GenerateSimGameUID());

                if (baySlot >= 0)
                {
                    Main.HBSLog.Log($"\tAdding {concreteMech.ChassisID} to bay {baySlot}");
                    simGame.AddMech(baySlot, concreteMech, true, true, false);
                }
                else
                {
                    Main.HBSLog.Log($"\tAdding {concreteMech.ChassisID} to storage, bays are full");
                    simGame.AddItemStat(concreteMech.Chassis.Description.Id, concreteMech.GetType(), false);
                }
            }
        }
    }
}
