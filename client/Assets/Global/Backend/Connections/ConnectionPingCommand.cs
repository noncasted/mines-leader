using Internal;
using Shared;

namespace Global.Backend
{
    public class ConnectionPingCommand : ResponseCommand<PingContext.Request>
    {
        protected override INetworkContext Execute(IReadOnlyLifetime lifetime, PingContext.Request context)
        {
            return PingContext.DefaultRequest;
        }
    }

    public static class ConnectionPingCommandExtensions
    {
        public static IScopeBuilder AddPingCommand(this IScopeBuilder builder)
        {
             builder.RegisterCommand<ConnectionPingCommand>();

             return builder;
        }
    }
}