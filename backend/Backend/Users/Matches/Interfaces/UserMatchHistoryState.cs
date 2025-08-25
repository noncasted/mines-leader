using Backend.Matches;
using Common;

// ReSharper disable CollectionNeverQueried.Global

namespace Backend.Users;

[Alias(States.User_MatchHistory)]
[GenerateSerializer]
public class UserMatchHistoryState
{
    [Id(0)] public List<MatchOverview> Matches { get; } = new();
}