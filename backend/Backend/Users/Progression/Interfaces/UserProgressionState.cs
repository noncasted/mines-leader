using Shared;

namespace Backend.Users;

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
        var total = 0;

        foreach (var record in Records)
            total += record.GetExperience();

        return total;
    }

    public INetworkContext ToContext() => new BackendUserContexts.ProgressionProjection()
    {
        Experience = CalculateTotal()
    };
}