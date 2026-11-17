using Shared;

namespace Meta.Matches;

public record MatchSetup(
    GameMatchType Type,
    DateTime StartDate,
    IReadOnlyList<Guid> Participants,
    Dictionary<Guid, IReadOnlyList<CardType>> ParticipantDecks,
    Dictionary<Guid, string> Nicknames);
