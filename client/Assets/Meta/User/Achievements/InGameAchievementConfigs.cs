using Shared;

namespace Meta
{
    public interface IInGameAchievementConfigs : IBackendProjection<InGameAchievementOptions>
    {
    }

    public class InGameAchievementConfigs : BackendProjection<InGameAchievementOptions>, IInGameAchievementConfigs
    {
    }
}