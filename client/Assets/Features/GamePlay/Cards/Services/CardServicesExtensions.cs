using Internal;
using Meta;
using Shared;

namespace GamePlay.Cards
{
    public static class CardServicesExtensions
    {
        public static IScopeBuilder AddCardServices(this IScopeBuilder builder)
        {
            builder.Register<CardFactory>()
                .WithAsset<CardFactoryOptions>()
                .As<ICardFactory>()
                .As<IScopeSetup>();

            builder.RegisterEnvDictionary<CardType, ICardDefinition, CardDefinition>();
            
            return builder;
        }
    }
}