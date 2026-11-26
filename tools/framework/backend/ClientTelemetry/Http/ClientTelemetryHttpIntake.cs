using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Game.ClientTelemetry;

// The telemetry of the clients that have no meta socket: POST /start, /batch, /end, /startup with the JSON of AddHttpClientTelemetry
// (CONTRACT.md, "HTTP"). The answers are those of the socket in camelCase: { enabled, sessionId }, { enabled }, {}, { enabled,
// startupId }. Knows no game and no authorization: the game maps it on its group and puts its own filter there, e.g.
// app.MapGroup("/api/telemetry").AddEndpointFilter(...).MapClientTelemetryHttpIntake(). Needs AddClientTelemetry on the host.
public static class ClientTelemetryHttpIntake
{
    public static RouteGroupBuilder MapClientTelemetryHttpIntake(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("");

        group.MapPost("/start", Start);
        group.MapPost("/batch", Batch);
        group.MapPost("/end", End);
        group.MapPost("/startup", Startup);

        return group;
    }

    // A request without a link is not of the client of the framework. A body that is not this JSON (NaN in a number) is 400 by the
    // binding of the framework.
    private static async Task<IResult> Start(HttpTelemetrySessionStart request, ClientTelemetryHttpLinks links)
    {
        if (request.LinkId == Guid.Empty)
            return Results.BadRequest();

        return Results.Json(await links.Of(request.LinkId).Start(TelemetryHttpMapping.ToSessionStart(request)));
    }

    private static async Task<IResult> Batch(HttpTelemetryBatch request, ClientTelemetryHttpLinks links)
    {
        if (request.LinkId == Guid.Empty)
            return Results.BadRequest();

        return Results.Json(await links.Of(request.LinkId).Accept(TelemetryHttpMapping.ToBatch(request)));
    }

    private static async Task<IResult> End(HttpTelemetrySessionEnd request, ClientTelemetryHttpLinks links)
    {
        if (request.LinkId == Guid.Empty)
            return Results.BadRequest();

        await links.Of(request.LinkId).End(TelemetryHttpMapping.ToSessionEnd(request));
        return Results.Json(new { });
    }

    private static async Task<IResult> Startup(HttpTelemetryStartupTrace request, ClientTelemetryHttpLinks links)
    {
        if (request.LinkId == Guid.Empty)
            return Results.BadRequest();

        return Results.Json(await links.Of(request.LinkId).AcceptStartup(TelemetryHttpMapping.ToStartupTrace(request), default));
    }
}
