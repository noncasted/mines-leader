#if UNITY_EDITOR
using Tools.PrefabBuilder;

namespace Startup
{
    [PrefabDefinition]
    public static class InternalScopePrefab
    {
        public static void Define(PrefabBuilder builder)
        {
            builder
                .WithName("Global/InternalScope")
                .WithComponent<InternalScope>();
        }
    }
}
#endif