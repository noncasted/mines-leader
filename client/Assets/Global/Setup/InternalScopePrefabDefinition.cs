#if UNITY_EDITOR
using Internal;
using Tools;

namespace Global.Setup
{
    [PrefabDefinition]
    public static class InternalScopePrefab
    {
        public static void Define(PrefabBuilder builder)
        {
            builder
                .WithName("InternalScope")
                .WithComponent<InternalScope>();
        }
    }
}
#endif
