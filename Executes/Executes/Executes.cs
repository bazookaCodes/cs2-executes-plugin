using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using CS2MenuManager.API.Menu;
using Executes.Configs;
using Executes.Enums;
using Executes.Managers;
using Executes.Models;

namespace Executes
{
    public class Executes : BasePlugin, IPluginConfig<ExecutesConfig>
    {
        #region Plugin Info
        public override string ModuleName => "Executes";
        public override string ModuleAuthor => "bazooka";
        public override string ModuleDescription => "Executes plugin for CS2.";
        public override string ModuleVersion => "0.0.1";
        #endregion

        #region Managers
        private GameManager? gameManager;
        private SpawnManager? spawnManager;
        private GrenadeManager? grenadeManager;
        private QueueManager? queueManager;
        #endregion

        private bool inDevMode = false;
        private Grenade? lastGrenade;
        private string messagePrefix = "Executes";
        private CsTeam lastRoundWinner = CsTeam.None;

        public ExecutesConfig Config { get; set; } = new ExecutesConfig();

        public override void Load(bool hotReload)
        {
            RegisterListener<Listeners.OnMapStart>(OnMapStart);
            RegisterListener<Listeners.OnEntitySpawned>(OnEntitySpawnedHandler);

            queueManager = new QueueManager();
            spawnManager = new SpawnManager();
            grenadeManager = new GrenadeManager();

            Console.WriteLine("Executes loaded.");

            AddCommandListener("jointeam", OnCommandJoinTeam);

            if (hotReload)
            {
                OnMapStart(Server.MapName);
            }
        }

        public override void Unload(bool hotReload)
        {
            RemoveListener("OnMapStart", OnMapStart);

            Console.WriteLine("Executes unloaded.");
        }

        public void OnConfigParsed(ExecutesConfig config)
        {
            Config = config;
        }

        private void OnMapStart(string map)
        {   
            gameManager = new GameManager(queueManager, ModuleDirectory, map);
            var loaded = gameManager!.LoadSpawns();

            if (!loaded)
            {
                Console.WriteLine("[Executes] Failed to load spawns.");
                return;
            }

            AddTimer(1.0f, () =>
            {
                Helpers.ExecuteExecutesConfiguration(ModuleDirectory);
            }, TimerFlags.STOP_ON_MAPCHANGE);
        }

        public void OnEntitySpawnedHandler(CEntityInstance entity)
        {
            if (!inDevMode) return;

            if (entity == null || entity.Entity == null) return;

            Server.NextFrame(() =>
            {
                CBaseCSGrenadeProjectile projectile = new CBaseCSGrenadeProjectile(entity.Handle);

                if (!projectile.IsValid ||
                    !projectile.Thrower.IsValid ||
                    projectile.Thrower.Value == null ||
                    projectile.Thrower.Value.Controller.Value == null ||
                    projectile.Globalname == "custom"
                ) return;

                CCSPlayerController player = new(projectile.Thrower.Value.Controller.Value.Handle);
                if (!player.IsValid || player.PlayerPawn.Value == null || !player.PlayerPawn.IsValid) return;
                int client = player.UserId!.Value;

                Vector position = new(projectile.AbsOrigin!.X, projectile.AbsOrigin.Y, projectile.AbsOrigin.Z);
                QAngle angle = new(projectile.AbsRotation!.X, projectile.AbsRotation.Y, projectile.AbsRotation.Z);
                Vector velocity = new(projectile.AbsVelocity.X, projectile.AbsVelocity.Y, projectile.AbsVelocity.Z);
                EGrenade nadeType = (EGrenade)entity.Entity.DesignerName.DesignerNameToEnum();

                lastGrenade = new Grenade {
                    Id = Guid.Empty,
                    Name = "last_grenade",
                    Type = nadeType,
                    Position = position,
                    Angle = angle,
                    Velocity = velocity,
                    Team = player.Team,
                    Delay = 0
                };

                player.ChatMessage(lastGrenade.ToString());

                //Replace the grenade since we're in debug mode
                Helpers.GivePlayerGrenade(player, nadeType);
            });
        }

        #region Event Handlers
        [GameEventHandler]
        public HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
        {
            Console.WriteLine("[Executes] EventHandler::OnRoundEnd");

            lastRoundWinner = (CsTeam)@event.Winner;

            return HookResult.Continue;
        }

        [GameEventHandler]
        public HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
        {
            var player = @event.Userid;

            if (!Helpers.IsValidPlayer(player) || !Helpers.IsPlayerConnected(player))
            {
                return HookResult.Continue;
            }

            // debug and check if the player is in the queue.
            Console.WriteLine($"[Executes] [{player.PlayerName}] Checking ActivePlayers.");
            if (!queueManager.ActivePlayers.Contains(player))
            {
                Console.WriteLine($"[Executes] [{player.PlayerName}] Checking player pawn {player.PlayerPawn.Value != null}.");

                if (player.PlayerPawn.Value != null && player.PlayerPawn.IsValid && player.PlayerPawn.Value.IsValid)
                {
                    Console.WriteLine($"[Executes] [{player.PlayerName}] player pawn is valid {player.PlayerPawn.IsValid} && {player.PlayerPawn.Value.IsValid}.");
                    Console.WriteLine($"[Executes] [{player.PlayerName}] calling playerpawn.commitsuicide()");
                    player.PlayerPawn.Value.CommitSuicide(false, true);
                }

                Console.WriteLine($"[Executes] [{player.PlayerName}] Player not in ActivePlayers, moving to spectator.");

                if (!player.IsBot)
                {
                    Console.WriteLine($"[Executes] [{player.PlayerName}] moving to spectator.");
                    player.ChangeTeam(CsTeam.Spectator);
                }

                return HookResult.Continue;
            }
            else
            {
                Console.WriteLine($"[Executes] [{player.PlayerName}] Player is in ActivePlayers.");
            }

            return HookResult.Continue;
        }

        [GameEventHandler]
        public HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
        {
            var attacker = @event.Attacker;
            var assister = @event.Assister;

            if (Helpers.IsValidPlayer(attacker))
            {
                gameManager.AddScore(attacker, GameManager.ScoreForKill);
            }

            if (Helpers.IsValidPlayer(assister))
            {
                gameManager.AddScore(assister, GameManager.ScoreForAssist);
            }

            return HookResult.Continue;
        }

        [GameEventHandler]
        public HookResult OnBombDefused(EventBombDefused @event, GameEventInfo info)
        {
            var player = @event.Userid;

            if (Helpers.IsValidPlayer(player))
            {
                gameManager.AddScore(player, GameManager.ScoreForDefuse);
            }

            return HookResult.Continue;
        }

        [GameEventHandler(HookMode.Pre)]
        public HookResult OnPlayerTeam(EventPlayerTeam @event, GameEventInfo info)
        {
            Console.WriteLine("[Executes] EventHandler::OnPlayerTeam");

            // Ensure all team join events are silent.
            @event.Silent = true;

            return HookResult.Continue;
        }

        private HookResult OnCommandJoinTeam(CCSPlayerController? player, CommandInfo commandInfo)
        {
            if (
            !Helpers.IsValidPlayer(player)
            || commandInfo.ArgCount < 2
            || !Enum.TryParse<CsTeam>(commandInfo.GetArg(1), out var toTeam)
        )
            {
                return HookResult.Handled;
            }

            var fromTeam = player!.Team;

            Console.WriteLine($"[Executes] [{player.PlayerName}] {fromTeam} -> {toTeam}");

            queueManager.DebugQueues(true);
            var response = queueManager.PlayerJoinedTeam(player, fromTeam, toTeam);
            queueManager.DebugQueues(false);

            Console.WriteLine($"[Executes] [{player.PlayerName}] checking to ensure we have active players");
            // If we don't have any active players, setup the active players and restart the game.
            if (queueManager.ActivePlayers.Count == 0)
            {
                Console.WriteLine($"[Executes] [{player.PlayerName}] clearing round teams to allow team changes");
                queueManager.ClearRoundTeams();

                Console.WriteLine($"[Executes] [{player.PlayerName}] no active players found, calling QueueManager.Update()");
                queueManager.DebugQueues(true);
                queueManager.Update();
                queueManager.DebugQueues(false);

                Helpers.RestartGame();
            }

            return response;
        }

        [GameEventHandler]
        public HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
        {
            Console.WriteLine("[Executes] EventHandler::OnPlayerConnectFull");

            var player = @event.Userid;

            if (player == null)
            {
                Console.WriteLine("[Executes] Failed to get player.");
                return HookResult.Continue;
            }

            player.TeamNum = (int)CsTeam.Spectator;
            player.ForceTeamTime = 3600.0f;

            // Create a timer to do this as it would occasionally fire too early.
            AddTimer(1.0f, () => player.ExecuteClientCommand("teammenu"));

            // TODO: Add the player to the queue
            // _queueManager._queue.Enqueue(player);            

            return HookResult.Continue;
        }

        [GameEventHandler]
        public HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
        {
            Console.WriteLine("[Executes] EventHandler::OnPlayerDisconnect");

            var player = @event.Userid;

            if (player == null)
            {
                Console.WriteLine("[Executes] Failed to get player.");
                return HookResult.Continue;
            }

            queueManager.RemovePlayerFromQueues(player);

            return HookResult.Continue;
        }

        [GameEventHandler]
        public HookResult OnRoundFreezeEnd(EventRoundFreezeEnd @event, GameEventInfo info)
        {
            Console.WriteLine("[Executes] EventHandler::OnRoundFreezeEnd");

            if (Helpers.IsWarmup())
            {
                Console.WriteLine("[Executes] Warmup detected, skipping.");
                return HookResult.Continue;
            }

            var currentScenario = gameManager.GetCurrentScenario();

            var gameRules = Helpers.GetGameRules();
            gameRules.RoundTime = currentScenario.RoundTime;

            var scenarioSite = currentScenario.Bombsite;
            var bombTargets = Utilities.FindAllEntitiesByDesignerName<CBombTarget>("func_bomb_target");
            foreach (var bombTarget in bombTargets)
            {
                //var disableOtherBombsiteOverride = Config.DisableOtherBombsiteOverride;
                //if (disableOtherBombsiteOverride.OverrideEnabled)
                //{
                //    if (!disableOtherBombsiteOverride.OverrideValue)
                //    {
                //        bombTarget.AcceptInput("Enable");
                //        continue;
                //    }
                //}
                //else if (!currentScenario.DisableOtherBombsite)
                //{
                //    bombTarget.AcceptInput("Enable");
                //    continue;
                //}

                if (scenarioSite == EBombsite.UNKNOWN)
                {
                    bombTarget.AcceptInput("Disable");
                    continue;
                }

                if (bombTarget.IsBombSiteB)
                {
                    bombTarget.AcceptInput(scenarioSite == EBombsite.B ? "Enable" : "Disable");
                }
                else
                {
                    bombTarget.AcceptInput(scenarioSite == EBombsite.B ? "Disable" : "Enable");
                }
            }

            return HookResult.Continue;
        }

        [GameEventHandler]
        public HookResult OnRoundPreStart(EventRoundPrestart @event, GameEventInfo info)
        {
            Console.WriteLine("[Executes] EventHandler::OnRoundPreStart");

            if (Helpers.IsWarmup())
            {
                Console.WriteLine("[Executes] Warmup detected, skipping.");
                return HookResult.Continue;
            }

            // Reset round teams to allow team changes.
            queueManager.ClearRoundTeams();

            // Update Queue status
            Console.WriteLine($"[Executes] Updating queues...");
            queueManager.DebugQueues(true);
            queueManager.Update();
            queueManager.DebugQueues(false);
            Console.WriteLine($"[Executes] Updated queues.");

            // Handle team swaps during round pre-start.
            switch (lastRoundWinner)
            {
                case CsTeam.CounterTerrorist:
                    Console.WriteLine($"[Executes] Calling CounterTerroristRoundWin()");
                    gameManager.CounterTerroristRoundWin();
                    Console.WriteLine($"[Executes] CounterTerroristRoundWin call complete");
                    break;

                case CsTeam.Terrorist:
                    Console.WriteLine($"[Executes] Calling TerroristRoundWin()");
                    gameManager.TerroristRoundWin();
                    Console.WriteLine($"[Executes] TerroristRoundWin call complete");
                    break;
            }

            gameManager.BalanceTeams();

            // Set round teams to prevent team changes mid round
            queueManager.SetRoundTeams();


            // Attempt to get a random scenario from the game manager
            var scenario = gameManager.GetRandomScenario();

            if (scenario == null)
            {
                Console.WriteLine("[Executes] Failed to get executes.");
                return HookResult.Continue;
            }

            return HookResult.Continue;
        }

        [GameEventHandler]
        public HookResult OnRoundPostStart(EventRoundPoststart @event, GameEventInfo info)
        {
            var hasBombBeenAllocated = false;

            Console.WriteLine($"[Executes] Trying to loop valid active players.");
            foreach (var player in queueManager.ActivePlayers.Where(Helpers.IsValidPlayer))
            {
                Console.WriteLine($"[Executes] [{player.PlayerName}] Adding timer for allocation...");

                if (!Helpers.IsValidPlayer(player))
                {
                    continue;
                }

                // Strip the player of all of their weapons and the bomb before any spawn / allocation occurs.
                //Helpers.RemoveHelmetAndHeavyArmour(player);
                player.RemoveWeapons();

                // Create a timer to do this as it would occasionally fire too early.
                AddTimer(0.05f, () =>
                {
                    if (!Helpers.IsValidPlayer(player))
                    {
                        Console.WriteLine($"[Executes] Allocating weapons: Player is not valid.");
                        return;
                    }

                    if (player.Team == CsTeam.Terrorist && !hasBombBeenAllocated)
                    {
                        hasBombBeenAllocated = true;
                        Console.WriteLine($"[Executes] Player is first T, allocating bomb.");
                        Helpers.GiveAndSwitchToBomb(player);
                    }

                    Console.WriteLine($"[Executes] Allocating...");
                    AllocationManager.Allocate(player);
                });
            }

            return HookResult.Continue;
        }

        [GameEventHandler]
        public HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
        {
            // TODO: FIGURE OUT WHY THE FUCK I NEED TO DO THIS
            var weirdAliveSpectators = Utilities.GetPlayers()
                .Where(x => x is { TeamNum: < (int)CsTeam.Terrorist, PawnIsAlive: true });
            foreach (var weirdAliveSpectator in weirdAliveSpectators)
            {
                // I **think** it's caused by auto team balance being on, so turn it off
                Server.ExecuteCommand("mp_autoteambalance 0");
                weirdAliveSpectator.CommitSuicide(false, true);
            }

            Console.WriteLine("[Executes] EventHandler::OnRoundStart");
            if (Helpers.IsWarmup())
            {
                Console.WriteLine("[Executes] Warmup detected, skipping.");
                return HookResult.Continue;
            }

            // Clear the round scores
            gameManager.ResetPlayerScores();

            // If we have a scenario then setup the players

            var currentScenario = gameManager.GetCurrentScenario();
            spawnManager.SetupSpawns(currentScenario);
            grenadeManager.SetupGrenades(currentScenario);

            if (currentScenario.Bombsite == EBombsite.UNKNOWN)
            {
                ChatHelpers.ChatMessageAll(currentScenario.Description, CsTeam.Terrorist);
                // ChatHelpers.ChatMessageAll($"Test: {currentScenario.Name}");
            }
            else
            {
                var description = currentScenario.Description.Replace("{{site}}", $"{ChatColors.Green}{currentScenario.Bombsite}{ChatColors.White}");
                ChatHelpers.ChatMessageAll(description, CsTeam.Terrorist);

                AddTimer(5.0f, () =>
                {
                    var CTPlayers = Utilities.GetPlayers().Where(x => x.Team == CsTeam.CounterTerrorist).ToList();
                    if (CTPlayers.Count == 0)
                    {
                        return;
                    }

                    var randPlayer = CTPlayers[Helpers.GetRandomInt(0, CTPlayers.Count - 1)];

                    if (randPlayer == null)
                    {
                        return;
                    }
                    randPlayer.ExecuteClientCommand($"say_team I think it's {ChatColors.Green}{currentScenario.Bombsite}{ChatColors.White}.");
                }, TimerFlags.STOP_ON_MAPCHANGE);
            }


            return HookResult.Continue;
        }
        #endregion

        #region Commands
        [ConsoleCommand("css_debug", "Allows the editing of spawns and grenades.")]
        [RequiresPermissions("@css/root")]
        public void OnToggleDevCommand(CCSPlayerController? player, CommandInfo commandInfo)
        {
            inDevMode = !inDevMode;

            Server.ExecuteCommand("mp_warmup_start");
            Server.ExecuteCommand("mp_warmuptime 120");
            Server.ExecuteCommand("mp_warmup_pausetimer 1");

            player?.ChatMessage($"Dev mode is now {inDevMode}");
        }

        [ConsoleCommand("css_listspawns", "Prints a list of spawns to the console.")]
        [RequiresPermissions("@css/root")]
        public void OnCommandListSpawns(CCSPlayerController? player, CommandInfo commandInfo)
        {
            if (!inDevMode)
            {
                commandInfo.ReplyToCommand("Command must be run in debug mode.");
                return;
            }

            if (gameManager == null)
            {
                commandInfo.ReplyToCommand($"Spawn manager is not loaded.");
                return;
            }

            player.PrintToConsole($"[Executes] -------- Spawns ------------");
            foreach (Spawn spawn in gameManager._mapConfig.Spawns)
            {
                player.PrintToConsole($"Side: {spawn.Team} Name: {spawn.Name}");
            }

            player.PrintToChat($"[Executes] Spawns have been printed to console.");
        }

        [ConsoleCommand("css_createscenario", "Create an execute scenario.")]
        [CommandHelper(minArgs: 3, usage: "[Name] [A/B] [Min Players]", whoCanExecute: CommandUsage.CLIENT_ONLY)]
        [RequiresPermissions("@css/root")]
        public void OnCommandCreateScenario(CCSPlayerController? player, CommandInfo commandInfo)
        {
            if (!inDevMode)
            {
                commandInfo.ReplyToCommand("Command must be run from debug mode.");
            }

            var scenarioName = commandInfo.GetArg(1);
            var bombsite = EBombsite.UNKNOWN;
            var minPlayers = int.Parse(commandInfo.GetArg(3));

            if (commandInfo.GetArg(2) == "A")
            {
                bombsite = EBombsite.A;
            }
            else
            {
                bombsite = EBombsite.B;
            }

            if (scenarioName == null || scenarioName == "")
            {
                commandInfo.ReplyToCommand("You must specify a name for the scenario.");
                return;
            }

            Scenario newScenario = new Scenario
            {
                Name = scenarioName,
                Bombsite = bombsite,
                RoundTime = 90,
                MinPlayerCount = minPlayers
            };

            var result = gameManager.AddScenario(newScenario);

            if (result)
            {
                commandInfo.ReplyToCommand($"[Executes] Scenario created.");
            }
            else
            {
                commandInfo.ReplyToCommand($"[Executes] Error creating scenario.");
            }
        }

        [ConsoleCommand("css_addtspawntoscenario", "Adds a spawn to a scenario.")]
        [RequiresPermissions("@css/root")]
        public void OnCommandAddTSpawnToScenario(CCSPlayerController? player, CommandInfo commandInfo)
        {
            if (!inDevMode)
            {
                commandInfo.ReplyToCommand("Command must be run from debug mode.");
                return;
            }

            WasdMenu scenarioMenu = new WasdMenu("Choose Scenario", this);

            foreach (Scenario scenario in gameManager._mapConfig.Scenarios)
            {
                scenarioMenu.AddItem(scenario.Name, (player, option) =>
                {
                    WasdMenu tSpawnMenu = new WasdMenu("Choose T Spawns", this);

                    foreach (Spawn spawn in gameManager._mapConfig.Spawns)
                    {
                        if (spawn.Team == CsTeam.Terrorist)
                        {
                            tSpawnMenu.AddItem(spawn.Name, (player, option) =>
                            {
                                option.PostSelectAction = CS2MenuManager.API.Enum.PostSelectAction.Nothing;

                                gameManager.AddSpawnToScenarioById(scenario.Name, spawn.Id);

                                player.ChatMessage($"Selected option {spawn.Name}");
                            });
                        }
                    }

                    tSpawnMenu.Display(player, 0);
                });
            }

            scenarioMenu.Display(player, 0);
        }

        [ConsoleCommand("css_addctspawntoscenario", "Adds a spawn to a scenario.")]
        [RequiresPermissions("@css/root")]
        public void OnCommandAddCTSpawnToScenario(CCSPlayerController? player, CommandInfo commandInfo)
        {
            if (!inDevMode)
            {
                commandInfo.ReplyToCommand("Command must be run from debug mode.");
                return;
            }

            WasdMenu scenarioMenu = new WasdMenu("Choose Scenario", this);

            foreach (Scenario scenario in gameManager._mapConfig.Scenarios)
            {
                scenarioMenu.AddItem(scenario.Name, (player, option) =>
                {
                    WasdMenu ctSpawnMenu = new WasdMenu("Choose CT Spawns", this);

                    foreach (Spawn spawn in gameManager._mapConfig.Spawns)
                    {
                        if (spawn.Team == CsTeam.CounterTerrorist)
                        {
                            ctSpawnMenu.AddItem(spawn.Name, (player, option) =>
                            {
                                option.PostSelectAction = CS2MenuManager.API.Enum.PostSelectAction.Nothing;

                                gameManager.AddSpawnToScenarioById(scenario.Name, spawn.Id);

                                player.ChatMessage($"Selected option {spawn.Name}");
                            });
                        }
                    }

                    ctSpawnMenu.Display(player, 0);
                });
            }

            scenarioMenu.Display(player, 0);
        }

        [ConsoleCommand("css_addgrenadetoscenario", "Adds a spawn to a scenario.")]
        [RequiresPermissions("@css/root")]
        public void OnCommandAddGrenadeToScenario(CCSPlayerController? player, CommandInfo commandInfo)
        {
            if (!inDevMode)
            {
                commandInfo.ReplyToCommand("Command must be run from debug mode.");
                return;
            }

            WasdMenu scenarioMenu = new WasdMenu("Choose Scenario", this);

            foreach (Scenario scenario in gameManager._mapConfig.Scenarios)
            {
                scenarioMenu.AddItem(scenario.Name, (player, option) =>
                {
                    WasdMenu grenadeMenu = new WasdMenu("Choose Grenades", this);

                    foreach (Grenade grenade in gameManager._mapConfig.Grenades)
                    {
                        grenadeMenu.AddItem(grenade.Name, (player, option) =>
                        {
                            option.PostSelectAction = CS2MenuManager.API.Enum.PostSelectAction.Nothing;

                            gameManager.AddGrenadeToScenarioById(scenario.Name, grenade.Id);

                            player.ChatMessage($"Selected option {grenade.Name}");
                        });
                    }

                    grenadeMenu.Display(player, 0);
                });
            }

            scenarioMenu.Display(player, 0);
        }

        [ConsoleCommand("css_addspawn", "Creates a new spawn for the bombsite currently shown.")]
        [CommandHelper(minArgs: 2, usage: "[Name] [T/CT]", whoCanExecute: CommandUsage.CLIENT_ONLY)]
        [RequiresPermissions("@css/root")]
        public void OnCommandAddSpawn(CCSPlayerController? player, CommandInfo commandInfo)
        {
            if (!inDevMode)
            {
                commandInfo.ReplyToCommand("Command must be run in debug mode.");
                return;
            }

            if (gameManager == null)
            {
                commandInfo.ReplyToCommand($"Spawn manager is not loaded.");
                return;
            }

            if (!player.IsValidPlayer())
            {
                commandInfo.ReplyToCommand($"You must have an alive player pawn.");
                return;
            }

            var name = commandInfo.GetArg(1);

            var spawnTeam = commandInfo.GetArg(2).ToUpper();
            CsTeam team = new CsTeam();
            if (spawnTeam == "T")
            {
                team = CsTeam.Terrorist;
            }
            else if (spawnTeam == "CT")
            {
                team = CsTeam.CounterTerrorist;
            }
            else
            {
                commandInfo.ReplyToCommand($"You must specify a team [T / CT] - [Value: {spawnTeam}].");
                return;
            }

            var spawns = gameManager._mapConfig.Spawns;

            var closestDistance = 9999.9;

            foreach (var spawn in spawns)
            {
                var distance = Helpers.GetDistanceBetweenVectors(spawn.Position, player!.PlayerPawn.Value!.AbsOrigin!);

                if (distance > 128.0 || distance > closestDistance)
                {
                    continue;
                }

                closestDistance = distance;
            }

            if (closestDistance <= 72)
            {
                commandInfo.ReplyToCommand($"You are too close to another spawn, move away and try again.");
                return;
            }

            var newSpawn = new Spawn {
                Id = Guid.NewGuid(),
                Name = name,
                Position =  player!.PlayerPawn.Value!.AbsOrigin!,
                Angle = player!.PlayerPawn.Value!.AbsRotation!,
                Team = team,
                Type = ESpawnType.SPAWNTYPE_NORMAL
            };
            Helpers.ShowSpawn(newSpawn);

            var spawnAdded = gameManager.AddSpawn(newSpawn);
            if (spawnAdded)
            {
                commandInfo.ReplyToCommand("[Executes] Spawn added.");
            }
            else
            {
                commandInfo.ReplyToCommand("[Executes] Error adding spawn.");
            }
        }

        [ConsoleCommand("css_addnade", "Saves the last nade thrown into the map config.")]
        [CommandHelper(minArgs: 2, usage: "[Name] [Delay (Seconds)]", whoCanExecute: CommandUsage.CLIENT_ONLY)]
        [RequiresPermissions("@css/root")]
        public void OnCommandAddNade(CCSPlayerController? player, CommandInfo commandInfo)
        {
            if (!inDevMode)
            {
                commandInfo.ReplyToCommand("Command must be run in debug mode.");
                return;
            }

            if (gameManager == null)
            {
                commandInfo.ReplyToCommand($"Spawn manager is not loaded.");
                return;
            }

            if (!player.IsValidPlayer())
            {
                commandInfo.ReplyToCommand($"You must have an alive player pawn.");
                return;
            }

            var name = commandInfo.GetArg(1);
            var delay = float.Parse(commandInfo.GetArg(2));

            Grenade newGrenade = new Grenade
            {
                Id = Guid.NewGuid(),
                Name = name,
                Type = lastGrenade.Type,
                Position = lastGrenade.Position,
                Angle = lastGrenade.Angle,
                Velocity = lastGrenade.Velocity,
                Team = lastGrenade.Team,
                Delay = delay
            };
            Helpers.ShowNade(newGrenade);

            var spawnAdded = gameManager.AddSpawn(newGrenade);
            if (spawnAdded)
            {
                commandInfo.ReplyToCommand("[Executes] Grenade added.");
            }
            else
            {
                commandInfo.ReplyToCommand("[Executes] Error adding grenade.");
            }
        }

        [ConsoleCommand("css_removespawn", "Removes the closest spawn.")]
        [RequiresPermissions("@css/root")]
        public void OnCommandRemoveSpawn(CCSPlayerController? player, CommandInfo commandInfo)
        {
            if (!inDevMode)
            {
                commandInfo.ReplyToCommand("Command must be run in debug mode.");
                return;
            }

            if (gameManager == null)
            {
                commandInfo.ReplyToCommand($"Spawn manager is not loaded.");
                return;
            }

            if (!player.IsValidPlayer())
            {
                commandInfo.ReplyToCommand($"You must have an alive player pawn.");
                return;
            }

            if (gameManager._mapConfig.Spawns.Count == 0)
            {
                commandInfo.ReplyToCommand($"No spawns found.");
                return;
            }

            var closestDistance = 9999.9;
            Spawn? closestSpawn = null;

            foreach (var spawn in gameManager._mapConfig.Spawns)
            {
                var distance = Helpers.GetDistanceBetweenVectors(spawn.Position, player!.PlayerPawn.Value!.AbsOrigin!);

                if (distance > 128.0 || distance > closestDistance)
                {
                    continue;
                }

                closestDistance = distance;
                closestSpawn = spawn;
            }

            if (closestSpawn == null)
            {
                commandInfo.ReplyToCommand($"No spawns found within 128 units.");
                return;
            }

            // Remove the beam entity that is showing for the closest spawn.
            var beamEntities = Utilities.FindAllEntitiesByDesignerName<CBeam>("beam");
            foreach (var beamEntity in beamEntities)
            {
                if (beamEntity.AbsOrigin == null)
                {
                    continue;
                }

                if (
                    beamEntity.AbsOrigin.Z - closestSpawn.Position.Z == 0 &&
                    beamEntity.AbsOrigin.X - closestSpawn.Position.X == 0 &&
                    beamEntity.AbsOrigin.Y - closestSpawn.Position.Y == 0
                )
                {
                    beamEntity.Remove();
                }
            }

            var spawnRemoved = gameManager.RemoveSpawn(closestSpawn);
            if (spawnRemoved)
            {
                commandInfo.ReplyToCommand("Spawn removed.");
            }
            else
            {
                commandInfo.ReplyToCommand("Error removing spawn.");
            }
        }

        [ConsoleCommand("css_removenade", "Removes the closest nade.")]
        [RequiresPermissions("@css/root")]
        public void OnCommandRemoveNade(CCSPlayerController? player, CommandInfo commandInfo)
        {
            if (!inDevMode)
            {
                commandInfo.ReplyToCommand("Command must be run in debug mode.");
                return;
            }

            if (gameManager == null)
            {
                commandInfo.ReplyToCommand($"Spawn manager is not loaded.");
                return;
            }

            if (!player.IsValidPlayer())
            {
                commandInfo.ReplyToCommand($"You must have an alive player pawn.");
                return;
            }

            if (gameManager._mapConfig.Grenades.Count == 0)
            {
                commandInfo.ReplyToCommand($"No grenades found.");
                return;
            }

            var closestDistance = 9999.9;
            Grenade? closestGrenade = null;

            foreach (var grenade in gameManager._mapConfig.Grenades)
            {
                var distance = Helpers.GetDistanceBetweenVectors(grenade.Position, player!.PlayerPawn.Value!.AbsOrigin!);

                if (distance > 128.0 || distance > closestDistance)
                {
                    continue;
                }

                closestDistance = distance;
                closestGrenade = grenade;
            }

            if (closestGrenade == null)
            {
                commandInfo.ReplyToCommand($"No grenades found within 128 units.");
                return;
            }

            // Remove the beam entity that is showing for the closest spawn.
            var beamEntities = Utilities.FindAllEntitiesByDesignerName<CBeam>("beam");
            foreach (var beamEntity in beamEntities)
            {
                if (beamEntity.AbsOrigin == null)
                {
                    continue;
                }

                if (
                    beamEntity.AbsOrigin.Z - closestGrenade.Position.Z == 0 &&
                    beamEntity.AbsOrigin.X - closestGrenade.Position.X == 0 &&
                    beamEntity.AbsOrigin.Y - closestGrenade.Position.Y == 0
                )
                {
                    beamEntity.Remove();
                }
            }

            var spawnRemoved = gameManager.RemoveSpawn(closestGrenade);
            if (spawnRemoved)
            {
                commandInfo.ReplyToCommand("Spawn removed.");
            }
            else
            {
                commandInfo.ReplyToCommand("Error removing spawn.");
            }
        }

        [ConsoleCommand("css_rethrow", "Rethrows the last grenade thrown.")]
        [RequiresPermissions("@css/root")]
        public void OnRethrowCommand(CCSPlayerController? player, CommandInfo commandInfo)
        {
            if (!inDevMode || lastGrenade == null || player == null) return;

            player.ChatMessage("Last grenade:");
            player.ChatMessage(lastGrenade.ToString());
            AddTimer(lastGrenade.Delay, () => lastGrenade.Throw());
        }

        [ConsoleCommand("css_throw", "Throws the specified name.")]
        [RequiresPermissions("@css/root")]
        public void OnCommandThrowGrenade(CCSPlayerController? player, CommandInfo commandInfo)
        {
            if (!inDevMode || !player.IsValidPlayer())
            {
                player.ChatMessage("Command only available in debug mode.");
            }

            WasdMenu grenadeMenu = new WasdMenu("Choose Grenade", this);

            foreach (Grenade grenade in gameManager._mapConfig.Grenades)
            {
                grenadeMenu.AddItem(grenade.Name, (player, option) =>
                {
                    player.ChatMessage($"Throwing grenade {grenade.Name}.");
                    AddTimer(grenade.Delay, () => grenade.Throw());
                });
            }

            grenadeMenu.Display(player, 0);
        }

        [ConsoleCommand("css_throwclosest", "Throws the closest grenade")]
        [RequiresPermissions("@css/root")]
        public void OnCommandThrowClosestGrenade(CCSPlayerController? player, CommandInfo commandInfo)
        {
            if (!inDevMode || !player.IsValidPlayer())
            {
                player.ChatMessage("Command only available in debug mode.");
            }

            if (gameManager._mapConfig.Grenades.Count == 0)
            {
                player.ChatMessage("No grenades exist.");
            }

            var closestDistance = 9999.9;
            Grenade? closestNade = null;

            foreach (Grenade grenade in gameManager._mapConfig.Grenades)
            {
                var distance = Helpers.GetDistanceBetweenVectors(grenade.Position, player!.PlayerPawn.Value!.AbsOrigin!);

                if (distance > 128.0 || distance > closestDistance)
                {
                    continue;
                }

                closestDistance = distance;
                closestNade = grenade;
            }

            if (closestNade == null)
            {
                commandInfo.ReplyToCommand($"[Executes] No grenades found within 128 units.");
                return;
            }

            player.ChatMessage($"Throwing grenade {closestNade.Name}.");
            AddTimer(closestNade.Delay, () => closestNade.Throw());
        }

        [ConsoleCommand("css_showspawns", "Creates visuals on the map to show all spawns.")]
        [RequiresPermissions("@css/root")]
        public void OnCommandShowSpawns(CCSPlayerController? player, CommandInfo commandInfo)
        {
            if (!inDevMode || !player.IsValidPlayer())
            {
                player.ChatMessage("Command only available in debug mode.");
            }

            var count = Helpers.ShowSpawns(gameManager._mapConfig.Spawns);
            player.ChatMessage($"Showing {count} spawns.");
        }

        [ConsoleCommand("css_shownades", "Creates visuals on the map to show all nades.")]
        [RequiresPermissions("@css/root")]
        public void OnCommandShowNades(CCSPlayerController? player, CommandInfo commandInfo)
        {
            if (!inDevMode || !player.IsValidPlayer())
            {
                player.ChatMessage("Command only available in debug mode.");
            }

            var count = Helpers.ShowNades(gameManager._mapConfig.Grenades);
            player.ChatMessage($"Showing {count} nades.");
        }

        [ConsoleCommand("css_runscenario", "Runs a specified scenario.")]
        [RequiresPermissions("@css/root")]
        public void OnCommandRunScenario(CCSPlayerController? player, CommandInfo commandInfo)
        {
            if (!inDevMode || !player.IsValidPlayer())
            {
                player.ChatMessage("Command only available in debug mode.");
                return;
            }

            WasdMenu scenarioMenu = new WasdMenu("Choose Scenario", this);

            foreach (Scenario scenario in gameManager._mapConfig.Scenarios)
            {
                scenarioMenu.AddItem(scenario.Name, (player, option) =>
                {
                    player.ChatMessage($"Executing scenario {scenario.Name}");

                    foreach (Grenade grenade in scenario.Grenades[CsTeam.Terrorist])
                    {
                        player.ChatMessage($"Throwing grenade {grenade.Name}.");
                        AddTimer(grenade.Delay, () => grenade.Throw());
                    }
                });
            }

            scenarioMenu.Display(player, 0);
        }

        [ConsoleCommand("css_getpos", "Prints the current position to the console")]
        [RequiresPermissions("@css/root")]
        public void OnCommandGetPos(CCSPlayerController? player, CommandInfo commandInfo)
        {
            if (!player.IsValidPlayer())
            {
                commandInfo.ReplyToCommand("[Executes] You must be a player to execute this command.");
                return;
            }

            player!.PrintToConsole("Current position:");
            player.PrintToConsole("---------------------------");
            player.PrintToConsole($"Pos: {player.PlayerPawn.Value!.AbsOrigin!.X} {player.PlayerPawn.Value.AbsOrigin.Y} {player.PlayerPawn.Value.AbsOrigin.Z}");
            player.PrintToConsole($"Eye: {player.PlayerPawn.Value.EyeAngles.X} {player.PlayerPawn.Value.EyeAngles.Y} {player.PlayerPawn.Value.EyeAngles.Z}");
            player.PrintToConsole("---------------------------");
        }
        #endregion
    }
}
