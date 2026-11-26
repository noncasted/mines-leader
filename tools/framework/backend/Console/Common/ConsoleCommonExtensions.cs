using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Console;

public static class ConsoleCommonExtensions
{
    public static IHostApplicationBuilder AddCommonConsoleComponents(this IHostApplicationBuilder builder)
    {
        builder.Services.AddScoped<IConsoleNavigation, ConsoleNavigation>();
        builder.Services.AddScoped<IClipboard, Clipboard>();
        builder.Services.AddScoped<IBrowserTime, BrowserTime>();

        return builder;
    }
}