using Meta.Matches;
using Shared;

namespace MetaGateway.Matches;

// A participant of a match in a row: the name at the setup, the deck, the rating change (0 without a winner).
public sealed record MatchHistorySeat
{
    public required Guid UserId { get; init; }
    public required string Nickname { get; init; }
    public required IReadOnlyList<CardType> Deck { get; init; }
    public required bool IsWinner { get; init; }
    public required int RatingChange { get; init; }

    public static MatchHistorySeat From(MatchState match, Guid userId)
    {
        return new MatchHistorySeat
        {
            UserId = userId,
            Nickname = match.NicknameOf(userId),
            Deck = match.ParticipantDecks[userId],
            IsWinner = match.Winner == userId,
            RatingChange = match.RatingChanges.GetValueOrDefault(userId)
        };
    }
}
