# CS2 Executes Plugin
### A plugin that allows for custom execute scenarios to be created and saved.

## Contribution

Feel free to send pull requests or just make the code your own. Im happy to merge any contributions anyone wants to make. I do not plan on making any further updates to the plugin.

## Installation

1. Download the [latest release](https://github.com/bazookaCodes/cs2-executes-plugin/releases/latest) of the plugin
2. Extract the Executes folder
3. Place the folder inside the plugins folder of CSSharp

## Commands

| Command | Arguments | Usage |   
|---|---|---|
| !debug | None | Pauses the match and enters editing mode. |   
| !listspawns | None | Prints a list of spawns to the console. |   
| !createscenario | [Name] [A/B] [Min Players] | Create an execute scenario. |
| !addtspawntoscenario | None | Adds a T spawn to a scenario. |  
| !addctspawntoscenario | None | Adds a CT spawn to a scenario. |
| !addgrenadetoscenario | None | Select from a menu a grenade to add to a scenario. |
| !addspawn | [Name] [T/CT] | Creates a new spawn entry. |
| !addnade | [Name] [Delay (Seconds)] | Creates a new grenade entry. |
| !removespawn | None | Use a menu to remove a given spawn entry. |
| !removenade | None | Use a menu to remove a given grenade entry. |
| !rethrow | None | Retrhows the most recently thrown grenade. |
| !throw | None | Pull up a menu to select a grenade to throw. |
| !throwclosest | None | Throws the closest grenade to current position. |
| !showspawns | None | Create visual highlights on the map for all spawn positions. |
| !hidespawns | None | Hides spawn indicators. |
| !shownades | None | Create visual highlights on the map for all grenade positions. |
| !hidenades | None | Hides grenade indicators. |
| !runscenario | None | Use a menu to throw all grenades for a chosen scenario. |
| !getpos | None | Prints the current position to the console. |

## Notes

- Map configs will be automatically created whenever a new map is started. No manual management of the configs needs to happen it will all be done using the menus.
- !debug will pause the round and enter a permanent warmup phase. All editing needs to be done in this phase.


## Development Packages
[CS2 Menu Manager](https://www.nuget.org/packages/CS2MenuManager)

- Used for creating the grenade menus in the plugin.

[CSSharp API](https://www.nuget.org/packages/CounterStrikeSharp.API)

## Resources

Guide for setting up a server: https://hub.tcno.co/games/cs2/dedicated_server/

CS Sharp Documentation: https://docs.cssharp.dev/docs/guides/getting-started.html

## Credits and shoutouts

[Executes](https://github.com/zwolof/cs2-executes)

- This was the inspiration for me to get started and I'm using much of the round handling and weapons allocation from this plugin.

[Retakes](https://github.com/B3none/cs2-retakes)

- I looked to this plugin for the visual spawn indicators and handling activating warmup mode for the debug menu.

[Matchzy](https://github.com/shobhit-pathak/MatchZy)

- I adapted the grenade spawning and projectile math from the practice mode of this plugin.
