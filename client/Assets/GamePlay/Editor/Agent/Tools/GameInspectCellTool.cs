using System.Threading.Tasks;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;

namespace GamePlay.Editor.Agent
{
    [McpForUnityTool("game_inspect_cell", Description = "Read the live Unity cell view (state, flag, minesAround, effects, animator). Does not hit the network.")]
    public static class GameInspectCellTool
    {
        public sealed class Parameters
        {
            [ToolParameter("Board X coordinate.")]
            public int x { get; set; }

            [ToolParameter("Board Y coordinate.")]
            public int y { get; set; }

            [ToolParameter("Inspect the opponent board instead of self.", Required = false, DefaultValue = "false")]
            public bool opponent { get; set; }
        }

        public static Task<object> HandleCommand(JObject parameters)
        {
            var x = GameAgentMcpTools.GetInt(parameters, "x", 0);
            var y = GameAgentMcpTools.GetInt(parameters, "y", 0);
            var opponent = GameAgentMcpTools.GetBool(parameters, "opponent", false);
            return GameAgentMcpTools.Read(agent => agent.InspectCell(x, y, opponent));
        }
    }
}
