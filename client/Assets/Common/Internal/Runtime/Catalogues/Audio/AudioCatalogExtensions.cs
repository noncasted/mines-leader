using Cysharp.Threading.Tasks;

namespace Internal
{
    public static class AudioCatalogExtensions
    {
        public static IScopeBuilder RequestAudioGroup(this IScopeBuilder builder, AudioGroup group)
        {
            return builder.RequestAssetGroup(group, "Audio");
        }

        public static UniTask LoadAudioGroup(this IScopeBuilder builder, AudioGroup group)
        {
            return builder.LoadAssetGroup(group, "Audio");
        }
    }
}
