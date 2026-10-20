using Shared;

namespace Internal.Tests
{
    // The writer serializes INetworkContext through the union formatter that the game registers at start
    // (Flow.Loop.UnionInitializer). Edit mode tests register the same union, in the same order, so the tags match;
    // registering it again replaces the formatter with an equal one.
    public static class TestUnions
    {
        public static void Register()
        {
            var contexts = new UnionBuilder<INetworkContext>();

            contexts
                .Add<EmptyResponse>()
                .AddSharedBackend()
                .AddSharedGame()
                .AddSharedSession();

            contexts.Build();
        }
    }
}
