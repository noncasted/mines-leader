using System.Threading.Tasks;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;

namespace GamePlay.Editor.Agent
{
    [McpForUnityTool("game_start_vs_bot", Description = "Wait until a GameMock LastManStandingTurnBased match is active, or return current status if one is already running.")]
    public static class GameStartVsBotTool
    {
        public static Task<object> HandleCommand(JObject _)
        {
            return GameAgentMcpTools.StartVsBot();
        }
    }
}
