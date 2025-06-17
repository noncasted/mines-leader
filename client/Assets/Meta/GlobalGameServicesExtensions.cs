using Global.GameServices;
using Internal;

namespace Assets.Meta
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
                .WithScopeLifetime()
                .As<IDeckService>()
                .As<IScopeSetup>();

            builder.RegisterScriptableRegistry<CardsRegistry, CardDefinition>()
                .As<ICardsRegistry>();
            
            return builder;
        }
    }
}