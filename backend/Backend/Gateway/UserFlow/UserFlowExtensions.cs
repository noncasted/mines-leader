using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Backend.Gateway;

public static class UserFlowExtensions
{
    public static IHostApplicationBuilder AddUserFlow(this IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        
        services.AddSignalR();
        services.AddSingleton<IConnectedUsers, ConnectedUsers>();
        services.AddHostedService<UserProjectionEntryPoint>();
        
        return builder;
    }
    
    public static IEndpointRouteBuilder MapUserHub(this IEndpointRouteBuilder builder)
    {
        builder.MapHub<UserHub>("/observer");
        
        return builder;
    }
}