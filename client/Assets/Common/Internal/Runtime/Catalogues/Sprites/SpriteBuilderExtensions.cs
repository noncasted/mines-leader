using Cysharp.Threading.Tasks;

namespace Internal
{
    public static class SpriteBuilderExtensions
    {
        public static IScopeBuilder RequestSpriteGroup(this IScopeBuilder builder, SpriteGroup group)
        {
            return builder.RequestAssetGroup(group, "Sprites");
        }

        public static UniTask LoadSpriteGroup(this IScopeBuilder builder, SpriteGroup group)
        {
            return builder.LoadAssetGroup(group, "Sprites");
        }
    }
}