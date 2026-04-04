using Internal;
using Meta;
using Shared;

namespace GamePlay.Cards
{
    public static class CardServicesExtensions
    {
        public static IScopeBuilder AddCardServices(this IScopeBuilder builder)
        {
            builder.Register<CardFactory>();

            return builder;
        }
    }
}