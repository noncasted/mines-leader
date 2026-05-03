#if UNITY_EDITOR
using Internal;
using Tools;
using Tools.Runtime.PrefabBuilder;

namespace Global.Setup
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