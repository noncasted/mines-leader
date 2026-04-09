using FluentAssertions;
using Meta.Matches;
using Meta.Users;
using Shared;
using Tests.Fixtures;
using Xunit;

namespace Tests.Meta;

[Collection(nameof(OrleansIntegrationCollection))]
public class UserRatingTests
    (OrleansTestClusterFixture fixture) : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    [Fact]
    public async Task AddRecord_WinRecord_IncreasesTotal()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<IUserRating>(id);
        await RunTransaction(() => grain.AddRecord(new UserRatingRecords.Win { Date = DateTime.UtcNow, Rating = 25 }));
        var total = 0;

        await RunTransaction(async () => {
            total = await grain.GetTotal();
        });
        total.Should().Be(25);
    }

    [Fact]
    public async Task AddRecord_LossRecord_DecreasesTotal()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<IUserRating>(id);
        await RunTransaction(() => grain.AddRecord(new UserRatingRecords.Loss { Date = DateTime.UtcNow, Rating = 15 }));
        var total = 0;

        await RunTransaction(async () => {
            total = await grain.GetTotal();
        });
        total.Should().Be(-15);
    }

    [Fact]
    public async Task AddRecord_WinThenLoss_NetRating()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<IUserRating>(id);
        await RunTransaction(() => grain.AddRecord(new UserRatingRecords.Win { Date = DateTime.UtcNow, Rating = 25 }));
        await RunTransaction(() => grain.AddRecord(new UserRatingRecords.Loss { Date = DateTime.UtcNow, Rating = 15 }));
        var total = 0;

        await RunTransaction(async () => {
            total = await grain.GetTotal();
        });
        total.Should().Be(10);
    }

    [Fact]
    public async Task AddRecord_MultipleWins_Accumulate()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<IUserRating>(id);

        for (var i = 0; i < 3; i++)
            await RunTransaction(() =>
                grain.AddRecord(new UserRatingRecords.Win { Date = DateTime.UtcNow, Rating = 25 }));
        var total = 0;

        await RunTransaction(async () => {
            total = await grain.GetTotal();
        });
        total.Should().Be(75);
    }

    [Fact]
    public async Task GetTotal_NoRecords_ReturnsZero()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<IUserRating>(id);
        var total = 0;

        await RunTransaction(async () => {
            total = await grain.GetTotal();
        });
        total.Should().Be(0);
    }

    [Fact]
    public async Task AddRecord_RatingCanGoNegative()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<IUserRating>(id);

        await RunTransaction(() => grain.AddRecord(new UserRatingRecords.Loss
            { Date = DateTime.UtcNow, Rating = 100 }));
        var total = 0;

        await RunTransaction(async () => {
            total = await grain.GetTotal();
        });
        total.Should().Be(-100);
    }

    [Fact]
    public async Task AddRecord_StatePersistsAcrossReferences()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<IUserRating>(id);
        await RunTransaction(() => grain.AddRecord(new UserRatingRecords.Win { Date = DateTime.UtcNow, Rating = 25 }));
        var grain2 = GetGrain<IUserRating>(id);
        var total = 0;

        await RunTransaction(async () => {
            total = await grain2.GetTotal();
        });
        total.Should().Be(25);
    }
}

[Collection(nameof(OrleansIntegrationCollection))]
public class UserProgressionTests
    (OrleansTestClusterFixture fixture) : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    [Fact]
    public async Task AddRecord_WinRecord_AddsExperience()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<IUserProgression>(id);

        await RunTransaction(() => grain.AddRecord(new UserProgressionRecords.Win
            { Date = DateTime.UtcNow, Experience = 100 }));
        var total = 0;

        await RunTransaction(async () => {
            total = await grain.GetTotal();
        });
        total.Should().Be(100);
    }

    [Fact]
    public async Task AddRecord_LossRecord_AddsPositiveExperience()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<IUserProgression>(id);

        await RunTransaction(() => grain.AddRecord(new UserProgressionRecords.Loss
            { Date = DateTime.UtcNow, Experience = 30 }));
        var total = 0;

        await RunTransaction(async () => {
            total = await grain.GetTotal();
        });
        total.Should().Be(30);
    }

    [Fact]
    public async Task AddRecord_WinAndLoss_BothAccumulate()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<IUserProgression>(id);

        await RunTransaction(() => grain.AddRecord(new UserProgressionRecords.Win
            { Date = DateTime.UtcNow, Experience = 100 }));

        await RunTransaction(() => grain.AddRecord(new UserProgressionRecords.Loss
            { Date = DateTime.UtcNow, Experience = 30 }));
        var total = 0;

        await RunTransaction(async () => {
            total = await grain.GetTotal();
        });
        total.Should().Be(130);
    }

    [Fact]
    public async Task GetTotal_NoRecords_ReturnsZero()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<IUserProgression>(id);
        var total = 0;

        await RunTransaction(async () => {
            total = await grain.GetTotal();
        });
        total.Should().Be(0);
    }

    [Fact]
    public async Task AddRecord_MultipleRecords_SumAll()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<IUserProgression>(id);

        for (var i = 0; i < 3; i++)
            await RunTransaction(() => grain.AddRecord(new UserProgressionRecords.Win
                { Date = DateTime.UtcNow, Experience = 100 }));

        for (var i = 0; i < 2; i++)
            await RunTransaction(() => grain.AddRecord(new UserProgressionRecords.Loss
                { Date = DateTime.UtcNow, Experience = 30 }));
        var total = 0;

        await RunTransaction(async () => {
            total = await grain.GetTotal();
        });
        total.Should().Be(360);
    }
}

[Collection(nameof(OrleansIntegrationCollection))]
public class UserAuthTests(OrleansTestClusterFixture fixture) : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    [Fact]
    public async Task IsExists_FreshGrain_ReturnsFalse()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<IUserAuth>(id);
        var exists = false;

        await RunTransaction(async () => {
            exists = await grain.IsExists();
        });
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task OnRegistered_SetsIsExistsTrue()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<IUserAuth>(id);
        await RunTransaction(() => grain.OnRegistered());
        var exists = false;

        await RunTransaction(async () => {
            exists = await grain.IsExists();
        });
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task OnRegistered_SetsRegistrationDate()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<IUserAuth>(id);
        var before = DateTime.UtcNow;
        await RunTransaction(() => grain.OnRegistered());
        var date = DateTime.MinValue;

        await RunTransaction(async () => {
            date = await grain.GetDate();
        });
        date.Should().BeOnOrAfter(before);
    }

    [Fact]
    public async Task OnRegistered_CalledTwice_StillExists()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<IUserAuth>(id);
        await RunTransaction(() => grain.OnRegistered());
        await RunTransaction(() => grain.OnRegistered());
        var exists = false;

        await RunTransaction(async () => {
            exists = await grain.IsExists();
        });
        exists.Should().BeTrue();
    }
}

[Collection(nameof(OrleansIntegrationCollection))]
public class UserDeckTests(OrleansTestClusterFixture fixture) : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    [Fact]
    public async Task Initialize_CreatesMaxDecksWithBaseDeck()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<IUserDeck>(id);
        await RunTransaction(() => grain.Initialize());
        UserDeckState? state = null;

        await RunTransaction(async () => {
            state = await grain.GetState();
        });
        state!.Entries.Should().HaveCount(DeckOptions.MaxDecks);

        foreach (var entry in state.Entries.Values)
            entry.Cards.Should().BeEquivalentTo(DeckOptions.BaseDeck);
    }

    [Fact]
    public async Task GetSelected_AfterInitialize_ReturnsBaseDeck()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<IUserDeck>(id);
        await RunTransaction(() => grain.Initialize());
        IReadOnlyList<CardType>? selected = null;

        await RunTransaction(async () => {
            selected = await grain.GetSelected();
        });
        selected.Should().BeEquivalentTo(DeckOptions.BaseDeck);
    }

    [Fact]
    public async Task Update_SingleDeck_PersistsCards()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<IUserDeck>(id);
        await RunTransaction(() => grain.Initialize());

        var customCards = new List<CardType>
        {
            CardType.Bloodhound, CardType.Bloodhound, CardType.Smoke, CardType.Smoke, CardType.OpponentBomb,
            CardType.OpponentBomb
        };
        await RunTransaction(() => grain.Update(1, customCards));
        UserDeckState? state = null;

        await RunTransaction(async () => {
            state = await grain.GetState();
        });
        state!.Entries[1].Cards.Should().BeEquivalentTo(customCards);
    }

    [Fact]
    public async Task Update_AllDecksAndSelectedIndex_Persisted()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<IUserDeck>(id);
        await RunTransaction(() => grain.Initialize());

        var customCards = new List<CardType>
            { CardType.Smoke, CardType.Smoke, CardType.Smoke, CardType.Smoke, CardType.Smoke, CardType.Smoke };

        var decks = new Dictionary<int, IReadOnlyList<CardType>>
            { [0] = customCards, [1] = customCards, [2] = customCards };
        await RunTransaction(() => grain.Update(decks, 2));
        UserDeckState? state = null;

        await RunTransaction(async () => {
            state = await grain.GetState();
        });
        state!.SelectedIndex.Should().Be(2);

        foreach (var entry in state.Entries.Values)
            entry.Cards.Should().BeEquivalentTo(customCards);
    }

    [Fact]
    public async Task GetSelected_AfterChangingSelectedIndex_ReturnsDifferentDeck()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<IUserDeck>(id);
        await RunTransaction(() => grain.Initialize());

        var customCards = new List<CardType>
            { CardType.Smoke, CardType.Smoke, CardType.Smoke, CardType.Smoke, CardType.Smoke, CardType.Smoke };
        await RunTransaction(() => grain.Update(1, customCards));
        var decks = new Dictionary<int, IReadOnlyList<CardType>>();
        await RunTransaction(() => grain.Update(decks, 1));
        IReadOnlyList<CardType>? selected = null;

        await RunTransaction(async () => {
            selected = await grain.GetSelected();
        });
        selected.Should().BeEquivalentTo(customCards);
    }

    [Fact]
    public async Task Initialize_OverwritesPreviousState()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<IUserDeck>(id);
        await RunTransaction(() => grain.Initialize());

        var customCards = new List<CardType>
            { CardType.Smoke, CardType.Smoke, CardType.Smoke, CardType.Smoke, CardType.Smoke, CardType.Smoke };
        await RunTransaction(() => grain.Update(0, customCards));
        await RunTransaction(() => grain.Initialize());
        UserDeckState? state = null;

        await RunTransaction(async () => {
            state = await grain.GetState();
        });
        state!.Entries[0].Cards.Should().BeEquivalentTo(DeckOptions.BaseDeck);
    }
}

[Collection(nameof(OrleansIntegrationCollection))]
public class UserMatchHistoryTests
    (OrleansTestClusterFixture fixture) : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    [Fact]
    public async Task Add_SingleMatch_StoredInHistory()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<IUserMatchHistory>(id);
        var overview = CreateOverview(id, Guid.NewGuid());
        await RunTransaction(() => grain.Add(overview));
        IReadOnlyList<MatchOverview>? block = null;

        await RunTransaction(async () => {
            block = await grain.GetBlock(10);
        });
        block.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetBlock_ReturnsLastNMatches()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<IUserMatchHistory>(id);

        for (var i = 0; i < 5; i++)
            await RunTransaction(() => grain.Add(CreateOverview(id, Guid.NewGuid())));
        IReadOnlyList<MatchOverview>? block = null;

        await RunTransaction(async () => {
            block = await grain.GetBlock(3);
        });
        block.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetBlock_MoreThanTotal_ReturnsAll()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<IUserMatchHistory>(id);
        await RunTransaction(() => grain.Add(CreateOverview(id, Guid.NewGuid())));
        await RunTransaction(() => grain.Add(CreateOverview(id, Guid.NewGuid())));
        IReadOnlyList<MatchOverview>? block = null;

        await RunTransaction(async () => {
            block = await grain.GetBlock(100);
        });
        block.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetBlock_EmptyHistory_ReturnsEmptyList()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<IUserMatchHistory>(id);
        IReadOnlyList<MatchOverview>? block = null;

        await RunTransaction(async () => {
            block = await grain.GetBlock(10);
        });
        block.Should().BeEmpty();
    }

    private static MatchOverview CreateOverview(Guid user1, Guid user2)
    {
        return new MatchOverview
        {
            Id = Guid.NewGuid(),
            Participants = [user1, user2],
            Date = DateTime.UtcNow,
            Winner = user1,
            Time = TimeSpan.FromSeconds(120),
            Type = GameMatchType.Single
        };
    }
}

[Collection(nameof(OrleansIntegrationCollection))]
public class MatchTests(OrleansTestClusterFixture fixture) : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    private async Task<(Guid user1, Guid user2)> SetupUsers()
    {
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        await RunTransaction(() => GetGrain<IUserDeck>(user1).Initialize());
        await RunTransaction(() => GetGrain<IUserDeck>(user2).Initialize());
        return (user1, user2);
    }

    [Fact]
    public async Task Setup_StoresTypeAndParticipants()
    {
        var (user1, user2) = await SetupUsers();
        var matchId = Guid.NewGuid();
        var match = GetGrain<IMatch>(matchId);
        await RunTransaction(() => match.Setup(GameMatchType.Single, new List<Guid> { user1, user2 }));
        MatchState? state = null;

        await RunTransaction(async () => {
            state = await match.GetState();
        });
        state!.Type.Should().Be(GameMatchType.Single);
        state.Participants.Should().BeEquivalentTo(new[] { user1, user2 });
    }

    [Fact]
    public async Task Setup_FetchesParticipantDecks()
    {
        var (user1, user2) = await SetupUsers();
        var matchId = Guid.NewGuid();
        var match = GetGrain<IMatch>(matchId);
        await RunTransaction(() => match.Setup(GameMatchType.Single, new List<Guid> { user1, user2 }));
        MatchState? state = null;

        await RunTransaction(async () => {
            state = await match.GetState();
        });
        state!.ParticipantDecks.Should().ContainKey(user1);
        state.ParticipantDecks.Should().ContainKey(user2);
        state.ParticipantDecks[user1].Should().BeEquivalentTo(DeckOptions.BaseDeck);
        state.ParticipantDecks[user2].Should().BeEquivalentTo(DeckOptions.BaseDeck);
    }

    [Fact]
    public async Task OnComplete_SetsWinnerAndDuration()
    {
        var (user1, user2) = await SetupUsers();
        var matchId = Guid.NewGuid();
        var match = GetGrain<IMatch>(matchId);
        await RunTransaction(() => match.Setup(GameMatchType.Single, new List<Guid> { user1, user2 }));
        await RunTransaction(() => match.OnComplete(user1));
        MatchState? state = null;

        await RunTransaction(async () => {
            state = await match.GetState();
        });
        state!.Winner.Should().Be(user1);
        state.Time.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task OnComplete_UpdatesWinnerProgression()
    {
        var (user1, user2) = await SetupUsers();
        var matchId = Guid.NewGuid();
        var match = GetGrain<IMatch>(matchId);
        await RunTransaction(() => match.Setup(GameMatchType.Single, new List<Guid> { user1, user2 }));
        await RunTransaction(() => match.OnComplete(user1));
        var total = 0;

        await RunTransaction(async () => {
            total = await GetGrain<IUserProgression>(user1).GetTotal();
        });
        total.Should().Be(100);
    }

    [Fact]
    public async Task OnComplete_UpdatesLoserProgression()
    {
        var (user1, user2) = await SetupUsers();
        var matchId = Guid.NewGuid();
        var match = GetGrain<IMatch>(matchId);
        await RunTransaction(() => match.Setup(GameMatchType.Single, new List<Guid> { user1, user2 }));
        await RunTransaction(() => match.OnComplete(user1));
        var total = 0;

        await RunTransaction(async () => {
            total = await GetGrain<IUserProgression>(user2).GetTotal();
        });
        total.Should().Be(30);
    }

    [Fact]
    public async Task OnComplete_UpdatesWinnerRating()
    {
        var (user1, user2) = await SetupUsers();
        var matchId = Guid.NewGuid();
        var match = GetGrain<IMatch>(matchId);
        await RunTransaction(() => match.Setup(GameMatchType.Single, new List<Guid> { user1, user2 }));
        await RunTransaction(() => match.OnComplete(user1));
        var total = 0;

        await RunTransaction(async () => {
            total = await GetGrain<IUserRating>(user1).GetTotal();
        });
        total.Should().Be(25);
    }

    [Fact]
    public async Task OnComplete_UpdatesLoserRating()
    {
        var (user1, user2) = await SetupUsers();
        var matchId = Guid.NewGuid();
        var match = GetGrain<IMatch>(matchId);
        await RunTransaction(() => match.Setup(GameMatchType.Single, new List<Guid> { user1, user2 }));
        await RunTransaction(() => match.OnComplete(user1));
        var total = 0;

        await RunTransaction(async () => {
            total = await GetGrain<IUserRating>(user2).GetTotal();
        });
        total.Should().Be(-15);
    }

    [Fact]
    public async Task OnComplete_StoresRatingChanges()
    {
        var (user1, user2) = await SetupUsers();
        var matchId = Guid.NewGuid();
        var match = GetGrain<IMatch>(matchId);
        await RunTransaction(() => match.Setup(GameMatchType.Single, new List<Guid> { user1, user2 }));
        await RunTransaction(() => match.OnComplete(user1));
        MatchState? state = null;

        await RunTransaction(async () => {
            state = await match.GetState();
        });
        state!.RatingChanges.Should().HaveCount(2);
        state.RatingChanges.Should().ContainKey(user1);
        state.RatingChanges.Should().ContainKey(user2);
        state.RatingChanges[user1].Should().Be(25);
        state.RatingChanges[user2].Should().Be(-15);
    }

    [Fact]
    public async Task OnComplete_AddsMatchToHistories()
    {
        var (user1, user2) = await SetupUsers();
        var matchId = Guid.NewGuid();
        var match = GetGrain<IMatch>(matchId);
        await RunTransaction(() => match.Setup(GameMatchType.Single, new List<Guid> { user1, user2 }));
        await RunTransaction(() => match.OnComplete(user1));
        IReadOnlyList<MatchOverview>? history1 = null;
        IReadOnlyList<MatchOverview>? history2 = null;

        await RunTransaction(async () => {
            history1 = await GetGrain<IUserMatchHistory>(user1).GetBlock(10);
        });

        await RunTransaction(async () => {
            history2 = await GetGrain<IUserMatchHistory>(user2).GetBlock(10);
        });
        history1.Should().HaveCount(1);
        history2.Should().HaveCount(1);
    }
}