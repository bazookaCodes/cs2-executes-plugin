using Executes.Models;

namespace Executes.Configs
{
    public class MapConfig
    {
        public List<Scenario> Scenarios { get; set; } = new();
        public List<Spawn> Spawns { get; set; } = new();

        public List<Grenade> Grenades { get; set; } = new();
    }
}