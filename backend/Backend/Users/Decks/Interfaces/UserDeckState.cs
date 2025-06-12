using Backend.Users.Projections;
using Shared;

namespace Backend.Users;

[GenerateSerializer]
public class UserDeckState : IProjectionPayload
{
    [Id(0)] public Dictionary<int, Entry> Entries { get; } = new();
    [Id(1)] public int SelectedIndex { get; set; }

    public INetworkContext ToContext()
    {
        return new BackendUserContexts.DeckProjection()
        {
            SelectedIndex = SelectedIndex,
            Entries = Entries.ToDictionary(
                entry => entry.Key,
                entry => new BackendUserContexts.DeckProjection.Entry
                {
                    DeckIndex = entry.Value.Index,
                    Cards = entry.Value.Cards
                })
        };
    }

    [GenerateSerializer]
    public class Entry
    {
        [Id(0)] public required int Index { get; init; }

        [Id(1)] public required IReadOnlyList<CardType> Cards { get; init; }
    }
}