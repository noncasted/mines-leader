using Common.Extensions;
using Game.GamePlay.CardPreviews;
using MetaGateway.UserFlow.Commands;
using MetaGateway.UserFlow.Connection;

namespace MetaGateway.UserFlow;

public static class UserFlowExtensions
{
    extension(IHostApplicationBuilder builder)
    {
        public IHostApplicationBuilder AddUserFlow()
        {
            var services = builder.Services;

            services.AddSingleton<IConnectedUsers, ConnectedUsers>();
            services.AddSingleton<IUserConnectionEntryPoint, UserConnectionEntryPoint>();
            services.AddSingleton<IUserCommandsCollection, UserCommandsCollection>();
            services.AddSingleton<IUserCommandsDispatcher, UserCommandsDispatcher>();
            services.AddSingleton<ICardPreviewGenerator, CardPreviewGenerator>();
            services.AddSingleton<MetaSocketAuth>();

            return builder;
        }

        public IHostApplicationBuilder AddUserCommand<T>()
            where T : class, IUserCommand
        {
            builder.Services.Add<IUserCommand, T>();
            return builder;
        }
    }
}