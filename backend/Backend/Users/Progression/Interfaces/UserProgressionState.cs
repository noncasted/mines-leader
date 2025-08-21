using Common;
using Shared;

namespace Backend.Users;

[Alias(States.User_Progression)]
[GenerateSerializer]
public class UserProgressionState : IProjectionPayload
{
    [Id(0)] public List<IUserProgressionRecord> Records { get; } = new();

    public void AddRecord(IUserProgressionRecord record)
    {
        Records.Add(record);
    }

    public int CalculateTotal()
    {
        return Records.Sum(total => total.GetExperience());
    }

    public INetworkContext ToContext() => new SharedBackendUser.ProgressionProjection()
    {
        Experience = CalculateTotal()
    };
}