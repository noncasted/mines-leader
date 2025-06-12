using Shared;

namespace Backend.Users;

public static class DeckOptions
{
    public const int MaxDecks = 3;
    public const int DeckSize = 6;
    
    public static readonly IReadOnlyList<CardType> BaseDeck = new List<CardType>
    {
        CardType.Bloodhound,
        CardType.ErosionDozer,
        CardType.Gravedigger,
        CardType.Trebuchet,
        CardType.TrebuchetAimer,
    };
}