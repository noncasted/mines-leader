using Backend.Matches;

namespace Backend.Users;

[GenerateSerializer]
public class UserMatchHistoryState
{
    [Id(0)] public List<MatchOverview> Matches { get; } = new();
}