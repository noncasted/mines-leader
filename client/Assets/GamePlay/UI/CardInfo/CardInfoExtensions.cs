using Internal;

namespace GamePlay.UI.CardInfo
{
    public static class CardInfoExtensions
    {
        public static IScopeBuilder AddCardInfoService(this IScopeBuilder builder)
        {
            builder.Register<CardInfoDisplayService>()
                   .As<ICardInfoDisplayService>();

            return builder;
        }
    }
}