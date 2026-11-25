using System.Threading.Tasks;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;

namespace GamePlay.Editor.Agent
{
    [McpForUnityTool("game_get_state", Description = "Return the last player-visible observation, or request an oracle rebuild when oracle is true.")]
    public static class GameGetStateTool
    {
        public sealed class Parameters
        {
            [ToolParameter("Rebuild from the server with the mines and the opponent hand visible. Served only against a bot with the cluster flag Cheats on a Development host, otherwise player-visible.", Required = false, DefaultValue = "false")]
            public bool oracle { get; set; }
        }

        public static Task<object> HandleCommand(JObject parameters)
        {
            if (GameAgentMcpTools.GetBool(parameters, "oracle", false))
                return GameAgentMcpTools.Run(agent => agent.RequestOracleState());

            return GameAgentMcpTools.Read(agent => agent.State());
        }
    }
}
