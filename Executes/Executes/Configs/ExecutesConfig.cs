using CounterStrikeSharp.API.Core;
using System.Text.Json.Serialization;

namespace Executes.Configs
{
    public class ExecutesConfig : BasePluginConfig
    {
        [JsonPropertyName("ChatMessagePrefix")]
        public string ChatMessagePrefix { get; set; } = "Executes";
    }
}
