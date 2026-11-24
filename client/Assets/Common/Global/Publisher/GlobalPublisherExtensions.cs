using System;
using Cysharp.Threading.Tasks;
using Global.Publisher.Itch;
using Internal;

namespace Global.Publisher
{
    public static class GlobalPublisherExtensions
    {
        // Reads the saves before the container is built: the services read them in their constructors.
        public static async UniTask AddPublisher(this IScopeBuilder builder)
        {
            var platform = InternalAssets.OptionsContainer.PlatformOptions.PlatformType;

            if (platform != PlatformType.Website && platform != PlatformType.ItchIO)
                throw new ArgumentOutOfRangeException(nameof(platform), platform, "[Publisher] No publisher for the platform");

            await builder.AddItchSaves();
        }
    }
}
