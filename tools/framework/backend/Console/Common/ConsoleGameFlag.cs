using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Console;

// A switch of the game (IClusterFeatures.GameFlags) shown on the Features page and in the status widget.
public class ConsoleGameFlag
{
    public required string Name { get; init; }
    public required string Label { get; init; }
    public required string Description { get; init; }

    // Short label of the home status tile.
    public required string StatusLabel { get; init; }
}

public static class ConsoleGameFlagExtensions
{
    public static IHostApplicationBuilder AddConsoleGameFlag(this IHostApplicationBuilder builder, ConsoleGameFlag flag)
    {
        builder.Services.AddSingleton(flag);
        return builder;
    }
}
