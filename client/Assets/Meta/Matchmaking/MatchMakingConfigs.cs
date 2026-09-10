using Shared;

namespace Meta
{
    public interface IMatchMakingConfigs : IBackendProjection<MatchMakingOptions>
    {
    }

    public class MatchMakingConfigs : BackendProjection<MatchMakingOptions>, IMatchMakingConfigs
    {
    }
}