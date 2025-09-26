using CounterStrikeSharp.API.Modules.Utils;
using Executes.Models.JsonConverters;
using Executes.Enums;
using System.Text.Json.Serialization;

namespace Executes.Models
{
    public class Spawn
    {
        public Guid? Id { get; set; }
        public string? Name { get; set; }

        [JsonConverter(typeof(VectorJsonConverter))]
        public Vector? Position { get; set; }

        [JsonConverter(typeof(QAngleJsonConverter))]
        public QAngle? Angle { get; set; }

        public CsTeam Team { get; set; }
        public ESpawnType Type { get; set; }
    }
}