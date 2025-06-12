using Global.Backend;
using Internal;
using Shared;

namespace Global.GameServices
{
    public static class GlobalGameServicesExtensions
    {
        public static IScopeBuilder AddGameServices(this IScopeBuilder builder)
        {
            builder.Register<LocalUsersService>()
                .As<IScopeSetup>();

            builder.Register<LocalUserList>()
                .As<ILocalUserList>()
                .AsSelf();

            builder.Register<UserContext>()
                .As<IUserContext>();

            builder.Register<GlobalContext>()
                .As<IGlobalContext>();

            builder.RegisterAsset<CharacterAvatars>();

            builder.Register<DeckService>()
                .As<IDeckService>()
                .WithScopeLifetime()
                .AsBackendProjection<BackendUserContexts.DeckProjection>();

            builder.RegisterScriptableRegistry<CardsRegistry, CardDefinition>()
                .As<ICardsRegistry>();
            
            return builder;
        }
    }
}