using Game.ClientTelemetry;

namespace MetaGateway.UserFlow.Commands;

public static class ClientTelemetryCommandsExtensions
{
    public static IHostApplicationBuilder AddClientTelemetryCommands(this IHostApplicationBuilder builder)
    {
        builder.AddClientTelemetry();
        builder.AddUserCommand<ClientTelemetryCommands.Start>();
        builder.AddUserCommand<ClientTelemetryCommands.Batch>();
        builder.AddUserCommand<ClientTelemetryCommands.End>();
        builder.AddUserCommand<ClientTelemetryCommands.StartupTrace>();

        return builder;
    }
}
