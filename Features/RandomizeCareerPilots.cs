using System;
using System.Collections.Generic;
using System.Linq;
using BattleTech;


namespace RandomCareerStart.Features
{
    class RandomizeCareerPilots
    {
        /// <summary>
        /// Recruits random pilots to the game instance
        /// </summary>
        /// <param name="simGame"></param>
        public static void TryRandomize(SimGameState simGame)
        {
            if (simGame.SimGameMode != SimGameState.SimGameType.CAREER)
                return;

            if (!Main.Settings.RandomizePilots)
                return;

            var numPilots = Main.Settings.NumberRoninFromList + Main.Settings.NumberRandomRonin +
                    Main.Settings.NumberProceduralPilots;
            if (numPilots <= 0)
            {
                Logger.LogWarning("Tried to randomize pilots but settings had total count of: {numPilots}");
                return;
            }

            ClearPilotList(simGame);

            // make up the numbers in the lesser types if there are not enough Ronin
            int shortage = AddRoninFromList(simGame, Main.Settings.StartingRonin, Main.Settings.NumberRoninFromList);
            shortage = AddRandomRonin(simGame, Main.Settings.NumberRandomRonin + shortage);
            AddProceduralPilots(simGame, Main.Settings.NumberProceduralPilots + shortage);

            if (Main.Settings.PilotUnspentExperienceBonus > 0)
            {
                foreach(var p in simGame.PilotRoster)
                {
                    p.AddExperience(0, "RandomCareerStart", Main.Settings.PilotUnspentExperienceBonus);
                }
            }
        }


        /// <summary>
        /// Clears the existing roster, without modifying the pilots.
        /// </summary>
        /// <param name="simGame"></param>
        public static void ClearPilotList(SimGameState simGame)
        {
            Logger.Log("Removing old pilots");
            // clear roster
            while (simGame.PilotRoster.Count > 0)
                simGame.PilotRoster.RemoveAt(0);
        }


        /// <summary>
        /// Add n randomly selected Ronin pilots from the given list to the roster
        /// </summary>
        /// <param name="simGame"></param>
        /// <param name="pilots">Whitelist of Ronin</param>
        /// <param name="n">How many to select</param>
        /// <returns>the number of pilots it was short by</returns>
        public static int AddRoninFromList(SimGameState simGame, List<String> pilots, int n)
        {
            List<string> _pilots = new List<string>(pilots);
            while (n > 0 && _pilots.Count > 0)
            {
                int i = _pilots.GetRandomIndex();
                var pilot = simGame.DataManager.PilotDefs.Get(_pilots[i]);
                if (pilot != null)
                {
                    if (simGame.AddPilotToRoster(pilot, true, true))
                    {
                        --n;
                        pilot.SetDayOfHire(simGame.DaysPassed);
                        Logger.Log($"\tAdded ronin from list: {pilot.Description.Id}");
                    }
                    else
                    {
                        Logger.LogError($"\tFailed to add Ronin from list: {pilot.Description.Id}");
                    }
                }
                else
                {
                    Logger.LogError($"\tSkipping ronin from list, does not exist: {_pilots[i]}");
                }
                _pilots.RemoveAt(i);
            }
            if (n > 0)
            {
                Logger.LogError($"\tList of ronin pilots was short by: {n}");
            }
            return n;
        }

        /// <summary>
        /// Add n randomly selected Ronin pilots to the roster
        /// </summary>
        /// <param name="simGame"></param>
        /// <param name="pilots">Whitelist of Ronin</param>
        /// <param name="n">How many to select</param>
        /// <returns>the number of pilots it was short by (should always be 0)</returns>
        public static int AddRandomRonin(SimGameState simGame, int n)
        {
            if (n <= 0)
                return n;

            // make sure to remove the starting ronin list from the possible random pilots! yay linq
            var randomRonin = simGame.RoninPilots
                    .Where(x => !Main.Settings.StartingRonin.Contains(x.Description.Id))
                    .ToList();
            // shuffle it
            randomRonin.Shuffle();
            foreach (var pilot in randomRonin)
            {
                // when we've added enough pilots, break out of the loop
                if (n <= 0)
                    break;
                // try to add a pilot
                if (simGame.AddPilotToRoster(pilot, true, true))
                {
                    --n;
                    pilot.SetDayOfHire(simGame.DaysPassed);
                    Logger.Log($"\tAdded random Ronin: {pilot.Description.Id}");
                }
                else
                {
                    Logger.Log($"\tFailed to add random Ronin: {pilot.Description.Id}");
                }
            }
            if (n > 0)
            {
                Logger.Log($"\tFailed to add enough random Ronin, needed another: {n}");
            }
            return n;
        }


        /// <summary>
        /// Add n randomly generated pilots to the roster
        /// </summary>
        /// <param name="simGame"></param>
        /// <param name="n">How many pilots</param>
        public static void AddProceduralPilots(SimGameState simGame, int n)
        {
            while (n > 0)
            {
                var pilots = simGame.PilotGenerator.GeneratePilots(n, Main.Settings.PilotPlanetDifficulty, 0f, out _);
                foreach (var pilot in pilots)
                {
                    if (!simGame.CanPilotBeCareerModeStarter(pilot))
                    {
                        Logger.Log($"\tProcedural pilot unsuitable for starting roster: {pilot.Description.Id}");
                        continue;
                    }
                    if (simGame.AddPilotToRoster(pilot, false, true))
                    {
                        --n;
                        pilot.SetDayOfHire(simGame.DaysPassed);
                        Logger.Log($"\tAdded procedural pilot: {pilot.Description.Id}");
                    }
                    else
                    {
                        Logger.Log($"\tFailed to add procedural pilot: {pilot.Description.Id}");
                    }
                }
            }
        }
    }
}
