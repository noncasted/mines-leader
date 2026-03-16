using Common.Extensions;
using Infrastructure;
using Microsoft.Extensions.Hosting;

namespace Meta.Bots;

public static class BotServicesExtensions
{
    public static IHostApplicationBuilder AddBotServices(this IHostApplicationBuilder builder)
    {
        builder.AddStateCollection<BotCollection, Guid, BotState>()
            .As<IBotCollection>();

        builder.Add<BotFactory>()
            .As<IBotFactory>();

        return builder;
    }
}