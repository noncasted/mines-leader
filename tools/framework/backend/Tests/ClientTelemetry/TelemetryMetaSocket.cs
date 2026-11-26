using Common.Extensions;
using Common.Network;
using Game.ClientTelemetry;
using Internal;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shared;

namespace Framework.Tests.ClientTelemetry;

// A meta socket without a game: the part of its dispatcher and commands the telemetry needs, on the real connection classes.
public static class TelemetryMetaSocket
{
    public const string Route = "/test-host/meta";

    public static void MapTelemetryMetaSocket(this IEndpointRouteBuilder endpoints, Action<INetworkContext>? onMessage = null)
    {
        endpoints.Map(Route, async (HttpContext context, IClientTelemetryConnections telemetry) => {
            if (!context.WebSockets.IsWebSocketRequest)
                return Results.BadRequest();

            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            var lifetime = new Lifetime();
            var connection = new Connection(socket, lifetime, NullLogger.Instance);

            connection.Reader.Requests.Advise(connection.Lifetime, request => Handle(connection, telemetry, request, onMessage).NoAwait());
            await connection.Run();
            lifetime.Terminate();

            return Results.Empty;
        });
    }

    private static async Task Handle(IConnection connection, IClientTelemetryConnections telemetry, RequestMessageFromClient request, Action<INetworkContext>? onMessage)
    {
        onMessage?.Invoke(request.Context);

        var session = telemetry.Of(connection.Lifetime);

        INetworkContext response = request.Context switch
        {
            SharedClientTelemetry.SessionStart start => await session.Start(start),
            SharedClientTelemetry.Batch batch => await session.Accept(batch),
            SharedClientTelemetry.SessionEnd end => await session.End(end),
            SharedClientTelemetry.StartupTrace trace => await session.AcceptStartup(trace, default),
            _ => ErrorResponse.UnknownCommand(request.Context)
        };

        connection.Writer.WriteResponse(response, request.RequestId);
    }
}
