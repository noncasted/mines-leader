using Shared;

namespace Meta
{
    public interface ICardConfigs : IBackendProjection<CardsConfigs>
    {
    }

    public class CardConfigs : BackendProjection<CardsConfigs>, ICardConfigs
    {
    }
}