using CounterStrikeSharp.API.Core;
using System.Text.Json.Serialization;

namespace Executes.Configs
{
    public class ExecutesConfig : BasePluginConfig
    {
        [JsonPropertyName("ChatMessagePrefix")]
        public string ChatMessagePrefix { get; set; } = "Executes";

        [JsonPropertyName("RoundWinScrambleEnabled")]
        public bool RoundWinScrambleEnabled { get; set; } = true;

        [JsonPropertyName("RoundWinScrambleRounds")]
        public int RoundWinScrambleRounds { get; set; } = 3;
    }
}
