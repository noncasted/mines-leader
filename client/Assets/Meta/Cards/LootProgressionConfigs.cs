using Shared;

namespace Meta
{
    public interface ILootProgressionConfigs : IBackendProjection<LootProgressionOptions>
    {
    }

    public class LootProgressionConfigs : BackendProjection<LootProgressionOptions>, ILootProgressionConfigs
    {
    }
}
