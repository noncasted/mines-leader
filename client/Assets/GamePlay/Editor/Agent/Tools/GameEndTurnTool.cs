using System.Threading.Tasks;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;

namespace GamePlay.Editor.Agent
{
    [McpForUnityTool("game_end_turn", Description = "Skip the remaining turn and wait for the next own turn or game over.")]
    public static class GameEndTurnTool
    {
        public sealed class Parameters
        {
            [ToolParameter("Milliseconds to wait for the next own turn.", Required = false, DefaultValue = "60000")]
            public int timeout_ms { get; set; }
        }

        public static Task<object> HandleCommand(JObject parameters)
        {
            var timeoutMs = GameAgentMcpTools.GetInt(parameters, "timeout_ms", 60000);
            return GameAgentMcpTools.Run(agent => agent.EndTurn(timeoutMs));
        }
    }
}
