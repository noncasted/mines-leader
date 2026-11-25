using System.Threading.Tasks;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;

namespace GamePlay.Editor.Agent
{
    [McpForUnityTool("game_status", Description = "Return whether a match is active and whose turn it is.")]
    public static class GameStatusTool
    {
        public static object HandleCommand(JObject _)
        {
            return GameAgentMcpTools.Status();
        }
    }
}
