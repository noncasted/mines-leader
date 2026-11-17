using Game.ClientTelemetry;
using MetaGateway.UserFlow.Connection;
using Shared;

namespace MetaGateway.UserFlow.Commands;

// The telemetry of the clients over the meta socket: the id of the user stays here, the framework gets only the lifetime of the socket.
public class ClientTelemetryCommands
{
    public class Start : UserCommand<SharedClientTelemetry.SessionStart>
    {
        public Start(IClientTelemetryConnections telemetry)
        {
            _telemetry = telemetry;
        }

        private readonly IClientTelemetryConnections _telemetry;

        protected override async Task<INetworkContext> Execute(IUserSession session, SharedClientTelemetry.SessionStart request)
        {
            return await _telemetry.Of(session.Lifetime).Start(request);
        }
    }

    public class Batch : UserCommand<SharedClientTelemetry.Batch>
    {
        public Batch(IClientTelemetryConnections telemetry)
        {
            _telemetry = telemetry;
        }

        private readonly IClientTelemetryConnections _telemetry;

        protected override async Task<INetworkContext> Execute(IUserSession session, SharedClientTelemetry.Batch request)
        {
            return await _telemetry.Of(session.Lifetime).Accept(request);
        }
    }

    public class End : UserCommand<SharedClientTelemetry.SessionEnd>
    {
        public End(IClientTelemetryConnections telemetry)
        {
            _telemetry = telemetry;
        }

        private readonly IClientTelemetryConnections _telemetry;

        protected override async Task<INetworkContext> Execute(IUserSession session, SharedClientTelemetry.SessionEnd request)
        {
            return await _telemetry.Of(session.Lifetime).End(request);
        }
    }

    public class StartupTrace : UserCommand<SharedClientTelemetry.StartupTrace>
    {
        public StartupTrace(IClientTelemetryConnections telemetry)
        {
            _telemetry = telemetry;
        }

        private readonly IClientTelemetryConnections _telemetry;

        protected override async Task<INetworkContext> Execute(IUserSession session, SharedClientTelemetry.StartupTrace request)
        {
            return await _telemetry.Of(session.Lifetime).AcceptStartup(request);
        }
    }
}
