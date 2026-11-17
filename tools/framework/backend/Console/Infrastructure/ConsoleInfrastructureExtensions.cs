using Console.Monitoring;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Console;

public static class ConsoleInfrastructureExtensions
{
    // The services of the infrastructure pages of the framework. The audit log reads the table AuditLogTable.Create makes.
    public static IHostApplicationBuilder AddConsoleInfrastructure(this IHostApplicationBuilder builder, ConsoleInfrastructureOptions options)
    {
        builder.Services.AddSingleton(options);
        builder.AddConsoleAuditLog();
        builder.Services.AddSingleton<IHeapReportStorage, HeapReportStorage>();
        builder.Services.AddSingleton<HeapReportCollector>();
        builder.Services.AddSingleton<SideEffectsAggregatorService>();
        builder.Services.AddSingleton<ISideEffectsAggregator>(sp => sp.GetRequiredService<SideEffectsAggregatorService>());
        builder.Services.AddHostedService(sp => sp.GetRequiredService<SideEffectsAggregatorService>());

        return builder;
    }

    // The audit log alone, for a console without the cluster (the test host of the telemetry).
    public static IHostApplicationBuilder AddConsoleAuditLog(this IHostApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IAuditLogStorage, AuditLogStorage>();
        return builder;
    }
}
