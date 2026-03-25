using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Console;

public static class ConsoleCommonExtensions
{
    public static IHostApplicationBuilder AddCommonConsoleComponents(this IHostApplicationBuilder builder)
    {
        builder.Services.AddScoped<IConsoleNavigation, ConsoleNavigation>();

        return builder;
    }
}
