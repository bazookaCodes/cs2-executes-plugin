using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using Executes.Configs;
using Executes.Enums;
using Executes.Managers;
using Executes.Models;
using System.ComponentModel.DataAnnotations;

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
            gameManager = new GameManager(queueManager);

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
            var loaded = gameManager!.LoadSpawns(ModuleDirectory, map);

            if (!loaded)
            {
                Console.WriteLine("[Executes] Failed to load spawns.");
                return;
            }

            AddTimer(1.0f, () =>
            {
                Helpers.ExecuteExecutesConfiguration(ModuleDirectory);
            }, TimerFlags.STOP_ON_MAPCHANGE);

            // Manually set time while testing to not count down
            Server.ExecuteCommand("mp_warmup_start");
            Server.ExecuteCommand("mp_warmuptime 120");
            Server.ExecuteCommand("mp_warmup_pausetimer 1");
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

                lastGrenade = new Grenade(
                    0,
                    "last_grenade",
                    player.Team,
                    position,
                    angle,
                    velocity,
                    player.PlayerPawn.Value.CBodyComponent!.SceneNode!.AbsOrigin,
                    player.PlayerPawn.Value.EyeAngles,
                    nadeType,
                    DateTime.Now
                );

                player.ChatMessage(lastGrenade.ToString());
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

            player?.ChatMessage($"Dev mode is now {inDevMode}");
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
