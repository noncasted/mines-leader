namespace Internal
{
    public static class SpriteAnimationExtensions
    {
        public static void RegisterSpriteForwardAnimation<T>(this IEntityBuilder builder, ForwardAnimationAsset asset)
            where T : ForwardSpriteAnimation
        {
            var data = new SpriteAnimationData(asset.Sprites, asset.Time, asset.Color);

            builder.Register<T>()
                   .As<IScopeSetup>()
                   .WithParameter<ISpriteAnimationData>(data);
        }

        public static void RegisterSpriteForwardAnimation<T>(this IEntityBuilder builder, ForwardAnimationData data)
            where T : ForwardSpriteAnimation
        {
            var animationData = new SpriteAnimationData(data.Sprites, data.Time, data.Color);

            builder.Register<T>()
                   .As<IScopeSetup>()
                   .WithParameter<ISpriteAnimationData>(animationData);
        }
    }
}
