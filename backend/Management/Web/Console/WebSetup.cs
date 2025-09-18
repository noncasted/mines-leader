using Aspire;
using Infrastructure.Discovery;
using Infrastructure.Messaging;
using Infrastructure.Orleans;
using Infrastructure.TaskScheduling;
using Management.Configs;
using MudBlazor.Services;
using ServiceLoop;
using Services;

namespace Management.Web;

public static class WebSetup
{
    public static IHostApplicationBuilder SetupWeb(this IHostApplicationBuilder builder)
    {
        var services = builder.Services;

        // Basic services
        builder
            .AddServiceDefaults()
            .AddOrleansClient();

        // Cluster services
        builder
            .AddEnvironment(ServiceTag.Console)
            .AddServiceLoop()
            .AddMessaging()
            .AddOrleansUtils()
            .AddServiceDiscovery()
            .AddConfigsServices()
            .AddTaskScheduling();

        // Project services
        services
            .AddMudServices()
            .AddRazorComponents()
            .AddInteractiveServerComponents();

        builder.AddCommonConsoleComponents();
        
        return builder;
    }
}