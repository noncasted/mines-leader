using Aspire;
using Common;
using Infrastructure.Discovery;
using Infrastructure.Messaging;
using Infrastructure.TaskScheduling;
using ServiceLoop;
using Services;

namespace Infrastructure.Orleans;

public static class SiloSetup
{
    public static IHostApplicationBuilder SetupSilo(this WebApplicationBuilder builder)
    {
        // Basic services
        builder
            .AddServiceDefaults()
            .AddOrleans();

        // Cluster services
        builder
            .AddEnvironment(ServiceTag.Silo)
            .AddServiceLoop()
            .AddMessaging()
            .AddOrleansUtils()
            .AddStateAttributes()
            .AddServiceDiscovery()
            .AddTaskScheduling();
        
        return builder;
    }
}