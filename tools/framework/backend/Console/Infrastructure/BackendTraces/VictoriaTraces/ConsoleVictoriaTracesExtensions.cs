using Console.Infrastructure.BackendTraces;
using Console.Infrastructure.BackendTraces.VictoriaTraces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Console;

public static class ConsoleVictoriaTracesExtensions
{
    // The backend traces of the console from VictoriaTraces of the observability stack (a deployed game): the startup page of a
    // client shows the trace of the authorization of its socket. One source per console: AddConsoleAspireDashboardTraces is the
    // other one.
    public static IHostApplicationBuilder AddConsoleVictoriaTraces(this IHostApplicationBuilder builder, VictoriaTracesOptions options)
    {
        BackendTraceRegistration.Add<VictoriaTracesReader>(builder.Services, VictoriaTracesReader.HttpClientName, options.BaseUrl, options.Timeout, _ => { });

        return builder;
    }

    // A section with Url; a missing value throws.
    public static IHostApplicationBuilder AddConsoleVictoriaTraces(this IHostApplicationBuilder builder, IConfigurationSection section)
    {
        return builder.AddConsoleVictoriaTraces(VictoriaTracesOptions.From(section));
    }
}
