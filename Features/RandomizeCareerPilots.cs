using System;
using System.Collections.Generic;
using System.Linq;
using BattleTech;


namespace RandomCareerStart.Features
{
    class RandomizeCareerPilots
    {
        public static void TryRandomize(SimGameState simGame)
        {
            if (simGame.SimGameMode != SimGameState.SimGameType.CAREER)
                return;

            if (!Main.Settings.RandomizePilots)
                return;

            // Commented out this block, an empty starting CAREER roster sounds reasonable to me
            //var numPilots = Main.Settings.StartingRonin.Count + Main.Settings.NumberRandomRonin +
            //        Main.Settings.NumberProceduralPilots;
            //if (numPilots <= 0)
            //{
            //    Main.HBSLog.LogWarning("Tried to randomize pilots but settings had 0!");
            //    return;
            //}

            ClearPilotList(simGame);
            AddRoninFromList(simGame, Main.Settings.StartingRonin, Main.Settings.NumberRoninFromList);
            AddRandomRonin(simGame, Main.Settings.NumberRandomRonin);
            AddProceduralPilots(simGame, Main.Settings.NumberProceduralPilots);
        }


        public static void ClearPilotList(SimGameState simGame)
        {
            Main.HBSLog.Log("Removing old pilots");
            // clear roster
            while (simGame.PilotRoster.Count > 0)
                simGame.PilotRoster.RemoveAt(0);
        }


        public static void AddRoninFromList(SimGameState simGame, List<String> pilots, int n)
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
                        Main.HBSLog.Log($"\tAdded ronin from list: {pilot.Description.Id}");
                    }
                    else
                    {
                        Main.HBSLog.Log($"\tFailed to add Ronin from list: {pilot.Description.Id}");
                    }
                }
                else
                {
                    Main.HBSLog.Log($"\tSkipping ronin from list, does not exist: {_pilots[i]}");
                }
                _pilots.RemoveAt(i);
            }
            if (n > 0)
            {
                Main.HBSLog.LogWarning($"\tList of ronin pilots was short by: {n}");
                //Logger.Debug($"List of pilots was short by: {n}");
            }
        }


        public static void AddRandomRonin(SimGameState simGame, int n)
        {
            if (n <= 0)
                return;

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
                    Main.HBSLog.Log($"\tAdded random Ronin: {pilot.Description.Id}");
                }
                else
                {
                    Main.HBSLog.Log($"\tFailed to add random Ronin: {pilot.Description.Id}");
                }
            }
            if (n > 0)
            {
                Main.HBSLog.Log($"\tFailed to add enough random Ronin, needed another: {n}");
            }
        }


        //Code for randomizing starting pilots
        public static void AddProceduralPilots(SimGameState simGame, int n)
        {
            var pilots = simGame.PilotGenerator.GeneratePilots(1, Main.Settings.PilotPlanetDifficulty, 0f, out _);
            int count = 0;
            foreach (var pilot in pilots)
            {
                if (!simGame.CanPilotBeCareerModeStarter(pilot))
                {
                    Main.HBSLog.Log($"\tProcedural pilot unsuitable for starting roster: {pilot.Description.Id}");
                    continue;
                }
                if (simGame.AddPilotToRoster(pilot, false, true))
                {
                    ++count;
                    pilot.SetDayOfHire(simGame.DaysPassed);
                    Main.HBSLog.Log($"\tAdded procedural pilot: {pilot.Description.Id}");
                }
                else
                {
                    Main.HBSLog.Log($"\tFailed to add procedural pilot: {pilot.Description.Id}");
                }
            }
            // recurse if we're short
            if (count < n)
                AddProceduralPilots(simGame, n - count);
        }
    }
}
