using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Backend.Matches;

public static class MatchServicesExtensions
{
    public static IHostApplicationBuilder AddBackendMatchServices(this IHostApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IMatchFactory, MatchFactory>();
        builder.Services.AddSingleton<ILobbyFactory, LobbyFactory>();
        
        return builder;
    }
    
    public static IHostApplicationBuilder AddGameMatchServices(this IHostApplicationBuilder builder)
    {
        builder.Services.AddHostedService<ServerSessions>();
        
        return builder;
    }
}