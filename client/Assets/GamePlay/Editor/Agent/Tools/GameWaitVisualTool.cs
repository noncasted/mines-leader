using System.Threading.Tasks;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;

namespace GamePlay.Editor.Agent
{
    [McpForUnityTool("game_wait_visual", Description = "Wait until no own-board cell animation is playing.")]
    public static class GameWaitVisualTool
    {
        public sealed class Parameters
        {
            [ToolParameter("Milliseconds to wait.", Required = false, DefaultValue = "5000")]
            public int timeout_ms { get; set; }
        }

        public static Task<object> HandleCommand(JObject parameters)
        {
            var timeoutMs = GameAgentMcpTools.GetInt(parameters, "timeout_ms", 5000);
            return GameAgentMcpTools.Run(agent => agent.WaitVisual(timeoutMs));
        }
    }
}
