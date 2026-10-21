using System;
using Global.Publisher.Itch;
using Internal;

namespace Global.Publisher
{
    public static class GlobalPublisherExtensions
    {
        public static IScopeBuilder AddPublisher(this IScopeBuilder builder)
        {
            var platform = InternalAssets.OptionsContainer.PlatformOptions.PlatformType;

            if (platform != PlatformType.Website && platform != PlatformType.ItchIO)
                throw new ArgumentOutOfRangeException(nameof(platform), platform, "[Publisher] No publisher for the platform");

            builder.AddItchSaves();

            return builder;
        }
    }
}
