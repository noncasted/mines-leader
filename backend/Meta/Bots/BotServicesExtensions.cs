using Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Meta.Bots;

public static class BotServicesExtensions {
    public static IHostApplicationBuilder AddBotServices(this IHostApplicationBuilder builder) {
        builder.AddAddressableDictionaryView<IBotCollectionView, BotCollectionView>();
        builder.Services.AddSingleton<IBotFactory, BotFactory>();
        return builder;
    }
}
