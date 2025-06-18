using System;
using Global.Publisher.Itch;
using Internal;

namespace Global.Publisher
{
    public static class GlobalPublisherExtensions
    {
        public static IScopeBuilder AddPublisher(this IScopeBuilder builder)
        {
            var platformOptions = builder.GetOptions<PlatformOptions>();

            switch (platformOptions.PlatformType)
            {
                case PlatformType.ItchIO:
                    AddItchIO(builder);
                    break;
                case PlatformType.Yandex:
                    break;
                case PlatformType.IOS:
                    break;
                case PlatformType.Android:
                    break;
                case PlatformType.CrazyGames:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return builder;
        }

        private static void AddItchIO(IScopeBuilder builder)
        {
            var platformOptions = builder.GetOptions<PlatformOptions>();
            var options = builder.GetAsset<GlobalPublisherOptions>();

            var callbacks = builder.Instantiate(options.ItchCallbacksPrefab);

            builder.RegisterInstance(callbacks)
                .As<IJsErrorCallback>();

            builder.Register<ItchSaves>()
                .As<ISaves>()
                .AsEventListener<IScopeBaseSetup>();

            builder.Register<ItchLanguageProvider>()
                .As<ISystemLanguageProvider>();

            if (platformOptions.IsEditor == true)
            {
                builder.Register<ItchLanguageDebugAPI>()
                    .As<IItchLanguageAPI>();
            }
            else
            {
                builder.Register<ItchLanguageExternAPI>()
                    .As<IItchLanguageAPI>();
            }
        }
    }
}