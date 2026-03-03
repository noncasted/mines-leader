using Shared;

namespace Meta
{
    public interface ICardConfigs : IBackendProjection<CardConfigOptions>
    {
    }

    public class CardConfigs : BackendProjection<CardConfigOptions>, ICardConfigs
    {
    }
}