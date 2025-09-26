using CounterStrikeSharp.API.Modules.Utils;
using Executes.Enums;
using System.Text.Json.Serialization;

namespace Executes.Models
{
    public class Scenario
    {
        public string? Name { get; set; }
        public string Description { get; set; } = "";
        public EBombsite Bombsite { get; set; }
        public bool DisableOtherBombsite { get; set; } = true;
        public int RoundTime { get; set; }
        public int MinPlayerCount { get; set; }
        public HashSet<Guid?> SpawnIds { get; set; } = new();
        public HashSet<Guid?> GrenadeIds { get; set; } = new();

        [JsonIgnore]
        public Dictionary<CsTeam, List<Spawn>> Spawns { get; set; } = new();

        [JsonIgnore]
        public Dictionary<CsTeam, List<Grenade>> Grenades { get; set; } = new();
    }
}