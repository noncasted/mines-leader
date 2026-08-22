using Shared;

namespace Meta.Matches;

public class MatchPayloads
{
    public class Match
    {
        [GenerateSerializer]
        public class Request
        {
            [Id(0)] public required GameMatchType Type { get; init; }
        }

        [GenerateSerializer]
        public class RequestWithBot
        {
            [Id(0)] public required GameMatchType Type { get; init; }
            [Id(1)] public required Guid BotId { get; init; }
            [Id(2)] public FixturePayload? Fixture { get; init; }
        }

        [GenerateSerializer]
        public class FixturePayload
        {
            [Id(0)] public string SelfBoardLayout { get; set; } = string.Empty;
            [Id(1)] public List<CardType> SelfHand { get; set; } = new();
            [Id(2)] public bool HumanGoesFirst { get; set; } = true;
            [Id(3)] public int? Mana { get; set; }
            [Id(4)] public int? Moves { get; set; }
            [Id(5)] public BotProfile? BotProfile { get; set; }
            [Id(6)] public List<CardType> SelfDeck { get; set; } = new();
            [Id(7)] public List<CardType> BotDeck { get; set; } = new();

            public static FixturePayload? From(AgentMatchFixture? fixture)
            {
                if (fixture == null)
                    return null;

                return new FixturePayload
                {
                    SelfBoardLayout = fixture.SelfBoardLayout ?? string.Empty,
                    SelfHand = fixture.SelfHand ?? new List<CardType>(),
                    HumanGoesFirst = fixture.HumanGoesFirst,
                    Mana = fixture.Mana,
                    Moves = fixture.Moves,
                    BotProfile = fixture.BotProfile,
                    SelfDeck = fixture.SelfDeck ?? new List<CardType>(),
                    BotDeck = fixture.BotDeck ?? new List<CardType>()
                };
            }

            public AgentMatchFixture ToShared()
            {
                return new AgentMatchFixture
                {
                    SelfBoardLayout = SelfBoardLayout ?? string.Empty,
                    SelfHand = SelfHand ?? new List<CardType>(),
                    HumanGoesFirst = HumanGoesFirst,
                    Mana = Mana,
                    Moves = Moves,
                    BotProfile = BotProfile,
                    SelfDeck = SelfDeck ?? new List<CardType>(),
                    BotDeck = BotDeck ?? new List<CardType>()
                };
            }
        }

        [GenerateSerializer]
        public class Response
        {
            [Id(0)] public required Guid SessionId { get; init; }
        }
    }

    public class Lobby
    {
        [GenerateSerializer]
        public class Request
        {
            [Id(0)] public required SessionType Type { get; init; }
            [Id(1)] public required Guid UserId { get; init; }
        }

        [GenerateSerializer]
        public class Response
        {
            [Id(0)] public required Guid SessionId { get; init; }
        }
    }
}