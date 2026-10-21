using System;
using Global.Publisher.Itch;
using Internal;

namespace Global.Publisher
{
    public static class GlobalPublisherExtensions
    {
        public static IScopeBuilder AddPublisher(this IScopeBuilder builder)
        {
            var platformOptions = InternalAssets.OptionsContainer.PlatformOptions;

            switch (platformOptions.PlatformType)
            {
                case PlatformType.Website:
                case PlatformType.ItchIO:
                    AddItchIO(builder);
                    break;
                case PlatformType.IOS:
                    break;
                case PlatformType.Android:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return builder;
        }

        private static void AddItchIO(IScopeBuilder builder)
        {
            builder.Register<ItchSaves>()
                   .As<ISaves>()
                   .As<IScopeBaseSetup>();
        }
    }
}