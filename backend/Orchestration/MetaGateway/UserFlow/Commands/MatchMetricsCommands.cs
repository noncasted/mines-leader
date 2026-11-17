using Game.MatchMetrics;
using MetaGateway.UserFlow.Connection;
using Shared;

namespace MetaGateway.UserFlow.Commands;

// The game data of matches over the meta socket: the user is the one of the session, the framework checks the rest.
public class MatchMetricsCommands
{
    public class Open : UserCommand<SharedMatchMetrics.Open>
    {
        public Open(IMatchMetricsConnections metrics)
        {
            _metrics = metrics;
        }

        private readonly IMatchMetricsConnections _metrics;

        protected override async Task<INetworkContext> Execute(IUserSession session, SharedMatchMetrics.Open request)
        {
            return await _metrics.Of(session.Lifetime, session.UserId).Open(request);
        }
    }

    public class Batch : UserCommand<SharedMatchMetrics.Batch>
    {
        public Batch(IMatchMetricsConnections metrics)
        {
            _metrics = metrics;
        }

        private readonly IMatchMetricsConnections _metrics;

        protected override async Task<INetworkContext> Execute(IUserSession session, SharedMatchMetrics.Batch request)
        {
            return await _metrics.Of(session.Lifetime, session.UserId).Accept(request);
        }
    }
}
