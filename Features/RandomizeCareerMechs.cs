using System.Collections.Generic;
using System.Linq;
using BattleTech;
using Harmony;

namespace RandomCareerStart.Features
{
    public class RandomizeCareerMechs
    {
        // which mechs are permitted by the settings
        private List<MechDef> AllowedMechs = new List<MechDef>();

        // temporary filtered lists
        private List<MechDef> PossibleMechs = new List<MechDef>();
        private List<MechDef> PossibleAssault = new List<MechDef>();
        private List<MechDef> PossibleHeavy = new List<MechDef>();
        private List<MechDef> PossibleMedium = new List<MechDef>();
        private List<MechDef> PossibleLight = new List<MechDef>();

        // how many lances to generate
        // success rate with default settings is >80% ...
        // so failing all 20 would be ~1 in 1e20 event...
        private int NUM_LANCES = 20;

        /// <summary>
        /// Adds a randomly generated lance to the game instance
        /// </summary>
        /// <param name="simGame"></param>
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
            // LogWarning("Tried to randomize mechs but settings had 0!");


            var randy = new RandomizeCareerMechs();

            // Build a lance exactly as specified, disregarding all other settings (except the RV limit)
            if (Main.Settings.UseLanceTonnageProfile)
            {
                // override json settings for max/min mech tonnage, so we don't filter out sizes required by the profile
                Main.Settings.MaximumMechTonnage = Main.Settings.LanceTonnageProfile.Max();
                Main.Settings.MinimumMechTonnage = Main.Settings.LanceTonnageProfile.Min();
                randy.FilterAllowedMechs(simGame);
                List<MechDef> lance = randy.GetLanceMatchingTonnageProfile(simGame, Main.Settings.LanceTonnageProfile);
                ApplyLance(simGame, lance);
                return;
            }

            // Use main algorithm for filtering and selecting the mechs

            var lances = new List<List<MechDef>>();
            randy.FilterAllowedMechs(simGame);

            for (int i = 0; i < randy.NUM_LANCES; ++i)
            {
                List<MechDef> lance = randy.GetRandomLance(simGame);
                lances.Add(lance);
            }
            lances = lances.OrderBy(lance => lance.Sum(mech => mech.Chassis.Tonnage)).ToList();
            Logger.Log($"Generated potential starting lances:");
            foreach (var lance in lances)
            {
                float lanceTonnage = lance.Sum(mech => mech.Chassis.Tonnage);
                Logger.Log($"\tLance size {lance.Count} with tonnage {lanceTonnage}: {string.Join(", ", lance.Select(mech => mech.ChassisID))}");
            }
            // pick out the lances that didn't blow the tonnage budget
            var optimalLances = lances.FindAll(lance => lance.Sum(mech => mech.Chassis.Tonnage) >= Main.Settings.MinimumLanceTonnage
                    && lance.Sum(mech => mech.Chassis.Tonnage) <= Main.Settings.MaximumLanceTonnage);
            if (optimalLances.Count > 0)
            {
                var selected = optimalLances.GetRandomIndex();
                Logger.Log($"SUCCESS: chosen lance {selected} = {string.Join(", ", optimalLances[selected].Select(mech => mech.ChassisID))}");
                ApplyLance(simGame, optimalLances[selected]);
            }
            // if there weren't any (very unlikely), just take the lightest lance
            else
            {
                Logger.LogError("ERROR: RandomCareerStart failed to find a lance subject to the given constraints!");
                ApplyLance(simGame, lances[0]);
            }
        }

        /// <summary>
        /// Easter Egg. If the ancestral mech is a FLEA, and the setting is enabled, then infest the ship...
        /// </summary>
        /// <param name="simGame"></param>
        private static void Infestation(SimGameState simGame)
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
                Logger.Log($"\tAdding {mech.ChassisID} to bay {i}");
                simGame.AddMech(i, mech, true, true, false);
            }
        }


        /// <summary>
        /// Derive the set of allowed mechs from which a lance might be derived
        /// </summary>
        /// <param name="simGame"></param>
        private void FilterAllowedMechs(SimGameState simGame)
        {
            if (Main.Settings.UseWhitelist)
            {
                AllowedMechs = new List<MechDef>();

                // remove items on whitelist that aren't in the datamanager
                Main.Settings.Whitelist.FindAll(id => !simGame.DataManager.MechDefs.Exists(id))
                        .Do(id => Logger.LogWarning($"\tInvalid MechDef '{id}' in whitelist. Will remove from possibilities"));
                Main.Settings.Whitelist.RemoveAll(id => !simGame.DataManager.MechDefs.Exists(id));
                AllowedMechs = Main.Settings.Whitelist.Select(id => simGame.DataManager.MechDefs.Get(id)).ToList();
            }
            else
            {
                // extract the mechdefs 
                var mechKeys = simGame.DataManager.MechDefs.Keys.ToList();
                Logger.Log($"\tPossible mech count initial: {mechKeys.Count}");

                // remove mechs from blacklist in settings
                if (Main.Settings.Debug && Main.Settings.DebugVerbose)
                    mechKeys.FindAll(id => Main.Settings.Blacklist.Contains(id))
                            .Do(id => Logger.LogVerbose($"\tRemoving blacklisted (by settings) MechDef '{id}' from possibilities"));
                mechKeys.RemoveAll(id => Main.Settings.Blacklist.Contains(id));
                Logger.Log($"\tPossible mech count after removing those in the blacklist (settings): {mechKeys.Count}");

                // remove mechs with undesirable labels (i.e. from the Skirmish MechBay
                if (Main.Settings.Debug && Main.Settings.DebugVerbose)
                    mechKeys.FindAll(id => id.Contains("CUSTOM"))
                            .Do(id => Logger.LogVerbose($"\tRemoving CUSTOM MechDef '{id}' from possibilities"));
                mechKeys.RemoveAll(id => id.Contains("CUSTOM"));
                Logger.Log($"\tPossible mech count after removing those with CUSTOM in name: {mechKeys.Count}");

                if (Main.Settings.Debug && Main.Settings.DebugVerbose)
                    mechKeys.FindAll(id => id.Contains("DUMMY"))
                            .Do(id => Logger.LogVerbose($"\tRemoving DUMMY MechDef '{id}' from possibilities"));
                mechKeys.RemoveAll(id => id.Contains("DUMMY"));
                Logger.Log($"\tPossible mech count after removing those with DUMMY in name: {mechKeys.Count}");

                // convert keys to mechdefs
                AllowedMechs = mechKeys.Select(id => simGame.DataManager.MechDefs.Get(id)).ToList();

                // remove mechs with undesirable tags
                if (Main.Settings.Debug && Main.Settings.DebugVerbose)
                    AllowedMechs.FindAll(mech => mech.MechTags.Contains("BLACKLISTED"))
                            .Do(mech => Logger.LogVerbose($"\tRemoving blacklisted (by tag) MechDef '{mech.ChassisID}' from possibilities"));
                AllowedMechs.RemoveAll(mech => mech.MechTags.Contains("BLACKLISTED"));
                Logger.Log($"\tPossible mech count after removing BLACKLISTED (by tag): {AllowedMechs.Count}");

                // remove mechs outside weight restrictions
                if (Main.Settings.Debug && Main.Settings.DebugVerbose)
                    AllowedMechs.FindAll(mech => mech.Chassis.Tonnage < Main.Settings.MinimumMechTonnage)
                            .Do(mech => Logger.LogVerbose($"\tRemoving underweight MechDef '{mech.ChassisID}' from possibilities"));
                AllowedMechs.RemoveAll(mech => mech.Chassis.Tonnage < Main.Settings.MinimumMechTonnage);
                Logger.Log($"\tPossible mech count after removing underweight: {AllowedMechs.Count}");

                if (Main.Settings.Debug && Main.Settings.DebugVerbose)
                    AllowedMechs.FindAll(mech => mech.Chassis.Tonnage > Main.Settings.MaximumMechTonnage)
                            .Do(mech => Logger.LogVerbose($"\tRemoving overweight MechDef '{mech.ChassisID}' from possibilities"));
                AllowedMechs.RemoveAll(mech => mech.Chassis.Tonnage > Main.Settings.MaximumMechTonnage);
                Logger.Log($"\tPossible mech count after removing overweight: {AllowedMechs.Count}");

                // remove mechs that don't exist yet
                if (Main.Settings.MechsAdhereToTimeline)
                {
                    var startDate = simGame.GetCampaignStartDate();
                    if (Main.Settings.Debug && Main.Settings.DebugVerbose)
                    {
                        AllowedMechs.FindAll(mech => mech.MinAppearanceDate.HasValue && mech.MinAppearanceDate > startDate)
                                .Do(mech => Logger.LogVerbose($"\tRemoving anachronistic MechDef '{mech.ChassisID}' from possibilities"));
                    }
                    AllowedMechs.RemoveAll(mech => mech.MinAppearanceDate.HasValue && mech.MinAppearanceDate > startDate);
                    Logger.Log($"\tPossible mech count after enforcing timeline: {AllowedMechs.Count}");
                }
            }

            // remove mechs with broken names (weird, but seen it once before)
            AllowedMechs.FindAll(mech => mech.Description.UIName == null)
                .Do(mech => Logger.LogError($"\tRemoving MechDef with null UIName '{mech.ChassisID}' from possibilities"));
            AllowedMechs.RemoveAll(mech => mech.Description.UIName == null);
            Logger.Log($"\tPossible mech count after removing null UIName: {AllowedMechs.Count}");

        }


        /// <summary>
        /// Remove a mech from the set of mechs currently being chosen from
        /// </summary>
        /// <param name="mech"></param>
        private void RemoveMech(MechDef mech)
        {
            PossibleAssault.Remove(mech);
            PossibleHeavy.Remove(mech);
            PossibleMedium.Remove(mech);
            PossibleLight.Remove(mech);
            PossibleMechs.Remove(mech);
        }


        /// <summary>
        /// Remove all mechs with the same base chassis name (e.g. all commandos) from the set of mechs currently being chosen from
        /// </summary>
        /// <param name="mech1"></param>
        private void RemoveChassis(MechDef mech1)
        {
            PossibleAssault.RemoveAll(mech2 => mech2.Name.Equals(mech1.Name));
            PossibleHeavy.RemoveAll(mech2 => mech2.Name.Equals(mech1.Name));
            PossibleMedium.RemoveAll(mech2 => mech2.Name.Equals(mech1.Name));
            PossibleLight.RemoveAll(mech2 => mech2.Name.Equals(mech1.Name));
            PossibleMechs.RemoveAll(mech2 => mech2.Name.Equals(mech1.Name));
        }


        /// <summary>
        /// Pick a mech at random, from the given list
        /// </summary>
        /// <param name="mechs"></param>
        /// <param name="mech"></param>
        /// <returns></returns>
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

        
        /// <summary>
        /// Pick a mech at random, from the given weight class
        /// </summary>
        /// <param name="weightClass"></param>
        /// <param name="mech"></param>
        /// <returns></returns>
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


        private List<MechDef> GetLanceMatchingTonnageProfile(SimGameState simGame, List<int> tonnageProfile)
        {
            PossibleMechs = new List<MechDef>(AllowedMechs);
            List<MechDef> lance = new List<MechDef>();
            float lanceTonnage = 0;
            bool filteredRV = false;
            foreach (int tonnage in tonnageProfile)
            {
                MechDef candidate = PossibleMechs.Where(mech => mech.Chassis.Tonnage == tonnage).GetRandomElement();
                if (candidate == null)
                {
                    Logger.LogError($"\tNo valid mechs left to choose from! It's spiders all the way down!");
                    candidate = simGame.DataManager.MechDefs.Get("mechdef_spider_SDR-5V");
                }
                AddMechToLance(candidate, lance);
                lanceTonnage += candidate.Chassis.Tonnage;

                // apply some dynamic filters:
                Logger.LogVerbose("\tchecking RV count");
                if (!filteredRV && Main.Settings.MaximumRVMechs <= lance.FindAll(mech => mech.Description.UIName.Contains("-RV")).Count)
                {
                    Logger.Log($"\tMax -RV reached, filtering out remaining -RV");
                    PossibleMechs.FindAll(mech => mech.Description.UIName.EndsWith("-RV"))
                            .Do(mech => Logger.LogVerbose($"\tRemoving RV MechDef '{mech.ChassisID}' from possibilities"));
                    PossibleMechs.RemoveAll(mech => mech.Description.UIName.Contains("-RV"));
                    filteredRV = true;
                }
            }
            Logger.Log($"\tSelected lance size {lance.Count} with tonnage {lanceTonnage}: {string.Join(", ", lance.Select(mech => mech.ChassisID))}");
            return lance;
        }


        /// <summary>
        /// Select a random lance, subject to the constraints given in the settings
        /// </summary>
        /// <param name="simGame"></param>
        /// <returns>A random lance, probably meeting the constraints</returns>
        private List<MechDef> GetRandomLance(SimGameState simGame)
        {
            PossibleMechs = new List<MechDef>(AllowedMechs);

            // sort possible mechs into buckets
            PossibleAssault = new List<MechDef>(PossibleMechs.FindAll(mech => mech.Chassis.weightClass == WeightClass.ASSAULT));
            PossibleHeavy = new List<MechDef>(PossibleMechs.FindAll(mech => mech.Chassis.weightClass == WeightClass.HEAVY));
            PossibleMedium = new List<MechDef>(PossibleMechs.FindAll(mech => mech.Chassis.weightClass == WeightClass.MEDIUM));
            PossibleLight = new List<MechDef>(PossibleMechs.FindAll(mech => mech.Chassis.weightClass == WeightClass.LIGHT));

            Logger.Log("Randomizing mechs, removing old mechs");

            List<MechDef> lance = new List<MechDef>();
            float lanceTonnage = 0;

            // add the ancestral mech
            {
                Logger.Log($"\tChoosing ancestral mech...");
                if (SelectAncestralMech(simGame, out MechDef mech))
                {
                    AddMechToLance(mech, lance);
                    lanceTonnage += mech.Chassis.Tonnage;
                }
            }

            // add more medium mechs if required
            while (lance.FindAll(mech => mech.Chassis.weightClass == WeightClass.MEDIUM).Count() < Main.Settings.MinimumMediumMechs)
            {
                Logger.Log($"\tPicking a medium mech...");
                if (SelectRandomMech(WeightClass.MEDIUM, out MechDef mech))
                {
                    AddMechToLance(mech, lance);
                    lanceTonnage += mech.Chassis.Tonnage;
                }
                else
                {
                    Logger.LogError($"\tFailed to add the required number of medium mechs");
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
                Logger.LogVerbose("\tchecking RV count");
                if (!filteredRV && Main.Settings.MaximumRVMechs <= lance.FindAll(mech => mech.Description.UIName.Contains("-RV")).Count)
                {
                    Logger.Log($"\tMax -RV reached, filtering out remaining -RV");
                    PossibleMechs.FindAll(mech => mech.Description.UIName.EndsWith("-RV"))
                            .Do(mech => Logger.LogVerbose($"\tRemoving RV MechDef '{mech.ChassisID}' from possibilities"));
                    PossibleMechs.RemoveAll(mech => mech.Description.UIName.Contains("-RV"));
                    filteredRV = true;
                }
                Logger.LogVerbose("\tchecking medium count");
                if (!filteredMedium && Main.Settings.MaximumMediumMechs <= lance.FindAll(mech => mech.Chassis.weightClass == WeightClass.MEDIUM).Count)
                {
                    Logger.Log($"\tMax mediums reached, filtering out remaining mediums");
                    PossibleMechs.FindAll(mech => mech.Chassis.weightClass == WeightClass.MEDIUM)
                            .Do(mech => Logger.LogVerbose($"\tRemoving Medium MechDef '{mech.ChassisID}' from possibilities"));
                    PossibleMechs.RemoveAll(mech => mech.Chassis.weightClass == WeightClass.MEDIUM);
                    filteredMedium = true;
                }
                Logger.LogVerbose("\tchecking ghetto count");
                if (!filteredGhetto && Main.Settings.MaximumGhettoMechs <= lance.FindAll(mech => mech.Chassis.Tonnage <= 20).Count)
                {
                    Logger.Log($"\tMax ghetto (20 tonners) reached, filtering out remaining 20 tonners");
                    PossibleMechs.FindAll(mech => mech.Chassis.Tonnage <= 20)
                            .Do(mech => Logger.LogVerbose($"\tRemoving ghetto (<=20 ton) MechDef '{mech.ChassisID}' from possibilities"));
                    PossibleMechs.RemoveAll(mech => mech.Chassis.Tonnage <= 20);
                    filteredGhetto = true;
                }

                SelectSuitableMechForLance(simGame, lance, out MechDef candidate);
                AddMechToLance(candidate, lance);
                lanceTonnage += candidate.Chassis.Tonnage;
            }
            Logger.Log($"\tSelected lance size {lance.Count} with tonnage {lanceTonnage}: {string.Join(", ", lance.Select(mech => mech.ChassisID))}");
            return lance;
        }


        /// <summary>
        /// Try to select a mech, such it will still be possible to satisfy the remaining constraints.
        /// Defaults to a Spider if that isn't possible.
        /// </summary>
        /// <param name="simGame"></param>
        /// <param name="lance">A partial lance to add to</param>
        /// <param name="candidate">The selected mech</param>
        /// <returns>Was the candidate suitable?</returns>
        private bool SelectSuitableMechForLance(SimGameState simGame, List<MechDef> lance, out MechDef candidate)
        {
            Logger.LogVerbose($"Entering SelectSuitableMechForLance");
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
                Logger.LogVerbose($"\tSelecting a mech with tonnage in the range [{minMechTonnage}, {maxMechTonnage}]");
                if (Main.Settings.Debug)
                {
                    PossibleMechs.FindAll(mech => mech.Chassis.Tonnage < minMechTonnage)
                            .Do(mech => Logger.LogVerbose($"\tRemoving underweight MechDef '{mech.ChassisID}' from possibilities"));
                    PossibleMechs.FindAll(mech => mech.Chassis.Tonnage > maxMechTonnage)
                            .Do(mech => Logger.LogVerbose($"\tRemoving overweight MechDef '{mech.ChassisID}' from possibilities"));
                }
                PossibleMechs.RemoveAll(mech => mech.Chassis.Tonnage < minMechTonnage);
                PossibleMechs.RemoveAll(mech => mech.Chassis.Tonnage > maxMechTonnage);
            }
            if (!SelectRandomMech(PossibleMechs, out candidate))
            {
                Logger.LogError($"\tNo valid mechs left to choose from! It's spiders all the way down!");
                candidate = simGame.DataManager.MechDefs.Get("mechdef_spider_SDR-5V");
                return false;
            }
            return true;
        }


        /// <summary>
        /// Fetch the mechdef for the ancestral mech, if one was chosen.
        /// If variants are permitted, selects a random mech of the same base chassis type
        /// </summary>
        /// <param name="simGame"></param>
        /// <param name="mech">The selected mech</param>
        /// <returns>Was a mech selected?</returns>
        private bool SelectAncestralMech(SimGameState simGame, out MechDef mech)
        {
            // see if the player (in BTR) selected an ancestral mech
            string ancestralMechKey = simGame.Constants.CareerMode.StartingPlayerMech;
            if (ancestralMechKey == "mechdef_centurion_TARGETDUMMY")
            {
                mech = null;
                return false;
            }

            if (!simGame.DataManager.MechDefs.Keys.Contains(ancestralMechKey))
            {
                Logger.LogError($"\tSelected career ancestral mech {ancestralMechKey} was not found in Datastore. Skipping.");
                mech = null;
                return false;
            }

            mech = simGame.DataManager.MechDefs.Get(ancestralMechKey);
            if (Main.Settings.RandomiseAncestralVariant == true)
            {
                string name = mech.Name;
                List<MechDef> variants = AllowedMechs.FindAll(mech2 => mech2.Name.Equals(name));
                //Logger.Log($"\t\tPrefix: {prefix}, resulted in matches: {variants.Count}");
                if (variants.Count > 0)
                {
                    mech = variants.GetRandomElement();
                }
                else
                {
                    Logger.LogError($"\tNo variants of the selected career ancestral mech are in the allowed list. Skipping variant randomisation.");
                }
            }
            return true;
        }


        /// <summary>
        /// Adds the mech to the given lance, then updates the filtered lists of available mechs if necessary.
        /// </summary>
        /// <param name="mech">The mech to add</param>
        /// <param name="lance">The lance to add it to</param>
        private void AddMechToLance(MechDef mech, List<MechDef> lance)
        {
            lance.Add(mech);
            Logger.Log($"\tSelected {mech.ChassisID} with tonnage {mech.Chassis.Tonnage}"); //, lance is now {lanceTonnage} tons");
            if (Main.Settings.AllowDuplicateMech == false)
                RemoveMech(mech);
            if (Main.Settings.AllowDuplicateChassis == false)
                RemoveChassis(mech);
        }


        /// <summary>
        /// Adds a lance to the player's roster (possibly to storage if space is insufficient)
        /// </summary>
        /// <param name="simGame"></param>
        /// <param name="lance"></param>
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
                    Logger.Log($"\tAdding {concreteMech.ChassisID} to bay {baySlot}");
                    simGame.AddMech(baySlot, concreteMech, true, true, false);
                }
                else
                {
                    Logger.Log($"\tAdding {concreteMech.ChassisID} to storage, bays are full");
                    simGame.AddItemStat(concreteMech.Chassis.Description.Id, concreteMech.GetType(), false);
                }
            }
        }
    }
}
