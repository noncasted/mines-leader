using System.Threading.Tasks;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;

namespace GamePlay.Editor.Agent
{
    [McpForUnityTool("game_wait_turn", Description = "Wait until it is the human turn or the match is over.")]
    public static class GameWaitTurnTool
    {
        public sealed class Parameters
        {
            [ToolParameter("Milliseconds to wait.", Required = false, DefaultValue = "60000")]
            public int timeout_ms { get; set; }
        }

        public static Task<object> HandleCommand(JObject parameters)
        {
            var timeoutMs = GameAgentMcpTools.GetInt(parameters, "timeout_ms", 60000);
            return GameAgentMcpTools.Run(agent => agent.WaitOwnTurn(timeoutMs));
        }
    }
}
