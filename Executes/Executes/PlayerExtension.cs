using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using Executes.Models;
using System;

namespace Executes
{
    public static class PlayerExtension
    {
        public static bool IsValidPlayer(this CCSPlayerController? player)
        {
            return player != null && player.IsValid && player.Connected == PlayerConnectedState.PlayerConnected;
        }

        public static void MoveToSpawn(this CCSPlayerController player, Spawn spawn)
        {
            if (player.PlayerPawn.Value == null || spawn.Position == null || spawn.Angle == null)
            {
                Console.WriteLine("[Executes] Spawn position or angle is null.");
                return;
            }

            Console.WriteLine($"[Executes] Moving {player.PlayerName} to {spawn.Name}.");

            player.PlayerPawn.Value.Teleport(spawn.Position, spawn.Angle, Vector.Zero);
        }
    }
}