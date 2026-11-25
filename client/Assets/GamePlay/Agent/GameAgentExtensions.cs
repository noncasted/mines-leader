using Internal;

namespace GamePlay.Agent
{
    public static class GameAgentExtensions
    {
        // The agent of the editor MCP tools; a release build has neither the services nor their classes.
        public static IScopeBuilder AddGameAgent(this IScopeBuilder builder)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            builder.Register<AgentObservations>()
                   .As<IScopeSetup>();

            builder.RegisterCommand<AgentObservationHandler>();

            builder.Register<AgentHandCards>();
            builder.Register<AgentBoardView>();

            builder.Register<GameAgent>()
                   .As<IScopeSetup>();
#endif
            return builder;
        }
    }
}
