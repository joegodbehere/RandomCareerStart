# RandomCampaignStart
Every campaign should feel different! This mod randomizes the starting Mechwarriors and 'Mechs, with settings to get exactly what you're looking for.

## Settings

### Pilots

|Setting|Description|Default|
|----|----|----|
|NumberProceduralPilots|Number of random pilots to give you|3|
|NumberRandomRonin|Number of random ronin to give you|1|
|NumberRoninFromList|Number of ronin to randomly select from the following list|1| 
|StartingRonin|A list of the Ronin/Kickstarter Mechwarriors PilotDef IDs that you'd like to be guaranteed to get|empty list|

### Mechs

|Setting|Description|Default|
|----|----|----|
|MinimumLanceSize|The minimum number of mechs that should be in the roster|5|
|MinimumLanceSize|The minimum number of mechs that should be in the roster|5|
|MaximumLanceTonnage|The maximum total mass of mechs in the roster|150|
|MimumumLanceTonnage|The minimum total mass of mechs in the roster|155|
|MechPercentageStartingCost|The percentage of the roster cost that should be deducted from the starting balance|0|
|MinimumMediumMechs|At least this many mechs will be Medium class mechs|1|
|MaximumMediumMechs|At most this many mechs will be Medium class mechs|1|
|RandomiseAncestralVariant|If an ancestral mech is specified, should it be rerolled to a different variant of that type?|true|
|MinimumMechTonnage|The minimal weight of any mech|45|
|MaximumMechTonnage|The maximal weight of any mech|20|
|AllowDuplicateChassis|Can the same chassis type appear more than once?|false|
|AllowDuplicateMech|Can the same variant appear more than once?|false|
|AllowCustomMech|Can customised mechs be selected?|false|
|MaximumRVMechs|Maximum number of -RV variant mechs|1|
|MaximuimGhettoMechs|Maximum number of 20 tonne mechs|1|
|MechsAdhereToTimeline|For BTR, are only timeline appropriate mechs selected|true|
|StartYear|Not currently used (the in game date is used instead)|3028|
|Whitelist|(Optional) List of 'Mechs that should be randomly chosen between|empty list []|
|Blacklist|List of 'Mechs that shouldn't be randomly chosen|empty list []|

### Dev settings

|Setting|Description|Default|
|----|----|----|
|Debug|Add debug messages to the main log|true|
|DebugVerbose|Add an excessive amount of debug messages|false|
|Infestation|????|true|

## Requirements

Requires [ModTek](https://github.com/BattletechModders/ModTek/releases). [Installation instructions for ModTek](https://github.com/BattleTechModders/ModTek/wiki/The-Drop-Dead-Simple-Guide-to-Installing-BTML-&-ModTek-&-ModTek-mods).
