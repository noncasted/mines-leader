#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Internal;
using Unity.Scripting.LifecycleManagement;

namespace GamePlay.Agent
{
    // The only static of the agent: MCP for Unity calls a tool through a public static HandleCommand(JObject)
    // found by reflection, outside every container, so the tool reaches the match through this field.
    [AutoStaticsCleanup]
    public static class GameAgentAccess
    {
        public static IGameAgent Current { get; private set; }
        public static bool IsActive => Current != null;

        public static IGameAgent Require()
        {
            if (Current == null)
                throw new InvalidOperationException(
                    "No agent match: enter Play with GameMock (mode LastManStandingTurnBased) and a running cluster");

            return Current;
        }

        public static void Attach(IReadOnlyLifetime lifetime, IGameAgent agent)
        {
            if (Current != null)
                throw new InvalidOperationException("Another agent match is still attached");

            Current = agent;
            lifetime.Listen(() => Current = null);
        }
    }
}
#endif
