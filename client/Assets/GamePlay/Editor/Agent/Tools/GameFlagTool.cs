using System.Threading.Tasks;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;

namespace GamePlay.Editor.Agent
{
    [McpForUnityTool("game_flag", Description = "Flag a cell. Legal during the opponent turn.")]
    public static class GameFlagTool
    {
        public sealed class Parameters
        {
            [ToolParameter("Board X coordinate.")]
            public int x { get; set; }

            [ToolParameter("Board Y coordinate.")]
            public int y { get; set; }
        }

        public static Task<object> HandleCommand(JObject parameters)
        {
            var x = GameAgentMcpTools.GetInt(parameters, "x", 0);
            var y = GameAgentMcpTools.GetInt(parameters, "y", 0);
            return GameAgentMcpTools.Run(agent => agent.Flag(x, y));
        }
    }
}
