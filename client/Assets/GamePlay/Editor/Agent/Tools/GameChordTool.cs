using System.Threading.Tasks;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;

namespace GamePlay.Editor.Agent
{
    [McpForUnityTool("game_chord", Description = "Chord-open a cell on the player's board.")]
    public static class GameChordTool
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
            return GameAgentMcpTools.Run(agent => agent.Chord(x, y));
        }
    }
}
