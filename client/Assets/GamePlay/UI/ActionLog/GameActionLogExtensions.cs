using Internal;

namespace GamePlay.UI.ActionLog
{
    public static class GameActionLogExtensions
    {
        public static IScopeBuilder AddActionLogService(this IScopeBuilder builder)
        {
            builder.Register<GameActionLog>()
                   .As<IGameActionLog>();

            return builder;
        }
    }
}