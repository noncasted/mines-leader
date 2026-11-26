using Common.Extensions;
using Game.GamePlay.CardPreviews;
using Infrastructure;
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
            services.Add<CardPreviewGenerator>()
                    .As<ICardPreviewGenerator>()
                    .As<ILocalSetupCompleted>();

            services.AddSingleton<MetaSocketAuth>();

            builder.AddTraceSource(MetaConnectionTelemetry.Source);

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