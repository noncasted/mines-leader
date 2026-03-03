using Shared;

namespace Meta.Users;

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
        CardType.ZipZap
    };

    public static readonly IReadOnlyList<CardType> BotPool = new List<CardType>
    {
        CardType.Bloodhound,
        CardType.Bloodhound_Max,
        CardType.ErosionDozer,
        CardType.ErosionDozer_Max,
        CardType.ZipZap,
        CardType.ZipZap_Max,
        CardType.OpponentBomb,
        CardType.Smoke,
        CardType.Smoke_Max,
    };
}