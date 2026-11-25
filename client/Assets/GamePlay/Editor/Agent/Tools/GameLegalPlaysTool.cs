using System.Threading.Tasks;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;

namespace GamePlay.Editor.Agent
{
    [McpForUnityTool("game_legal_plays", Description = "Ask the server which hand cards can be played and which cells they can target.")]
    public static class GameLegalPlaysTool
    {
        public static Task<object> HandleCommand(JObject _)
        {
            return GameAgentMcpTools.Run(agent => agent.LegalPlays());
        }
    }
}
