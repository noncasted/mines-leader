using Cluster.Configs;
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
    public async Task GetProjection_NoRecords_ReturnsZeroRating()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<IUserRating>(id);
        IProjectionPayload? projection = null;

        await RunTransaction(async () => {
            projection = await grain.GetProjection();
        });

        projection.Should().NotBeNull();
        var context = projection!.ToContext();
        var ratingProjection = context.Should().BeOfType<SharedBackendUser.RatingProjection>().Subject;
        ratingProjection.Rating.Should().Be(0);
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
public class UserStatsTests
    (OrleansTestClusterFixture fixture) : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    [Fact]
    public async Task Apply_SingleDelta_AccumulatesCounter()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<IUserStats>(id);

        await RunTransaction(() => grain.Apply(UserStatsDelta.Single(UserStatType.FlagsSet, 10)));

        UserStatsState? state = null;
        await RunTransaction(async () => {
            state = await grain.GetState();
        });

        state!.Get(UserStatType.FlagsSet).Should().Be(10);
    }

    [Fact]
    public async Task Apply_MultipleDeltas_SumUp()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<IUserStats>(id);

        await RunTransaction(() => grain.Apply(UserStatsDelta.Single(UserStatType.CellsOpened, 5)));
        await RunTransaction(() => grain.Apply(UserStatsDelta.Single(UserStatType.CellsOpened, 7)));

        UserStatsState? state = null;
        await RunTransaction(async () => {
            state = await grain.GetState();
        });

        state!.Get(UserStatType.CellsOpened).Should().Be(12);
    }

    [Fact]
    public async Task Apply_CardGroups_TrackedSeparately()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<IUserStats>(id);

        var delta = new UserStatsDelta();
        delta.AddCardPlayed(CardGroup.Attack);
        delta.AddCardPlayed(CardGroup.Attack);
        delta.AddCardPlayed(CardGroup.Scout);

        await RunTransaction(() => grain.Apply(delta));

        UserStatsState? state = null;
        await RunTransaction(async () => {
            state = await grain.GetState();
        });

        state!.GetCardsPlayed(CardGroup.Attack).Should().Be(2);
        state.GetCardsPlayed(CardGroup.Scout).Should().Be(1);
        state.GetCardsPlayed(CardGroup.Buff).Should().Be(0);
    }

    [Fact]
    public async Task GetState_NoStats_ReturnsZero()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<IUserStats>(id);

        UserStatsState? state = null;
        await RunTransaction(async () => {
            state = await grain.GetState();
        });

        state!.Get(UserStatType.FlagsSet).Should().Be(0);
    }

    [Fact]
    public async Task Reset_ClearsCounters()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<IUserStats>(id);

        await RunTransaction(() => grain.Apply(UserStatsDelta.Single(UserStatType.FlagsSet, 3)));
        await RunTransaction(() => grain.Reset());

        UserStatsState? state = null;
        await RunTransaction(async () => {
            state = await grain.GetState();
        });

        state!.Get(UserStatType.FlagsSet).Should().Be(0);
    }
}

[Collection(nameof(OrleansIntegrationCollection))]
public class UserInGameAchievementsTests
    (OrleansTestClusterFixture fixture) : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    [Fact]
    public async Task Evaluate_BelowThreshold_UnlocksNothing()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<IUserInGameAchievements>(id);
        var stats = GetGrain<IUserStats>(id);

        await RunTransaction(() => stats.Apply(UserStatsDelta.Single(UserStatType.FlagsSet, 1)));
        await RunTransaction(() => grain.Evaluate());

        IReadOnlyList<InGameAchievementEntry>? unlocked = null;
        await RunTransaction(async () => {
            unlocked = await grain.GetUnlocked();
        });

        unlocked!.Any(e => e.Type == InGameAchievementType.FlagsSet).Should().BeFalse();
    }

    [Fact]
    public async Task Evaluate_CrossesFirstTier_UnlocksOnce()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<IUserInGameAchievements>(id);
        var stats = GetGrain<IUserStats>(id);

        await RunTransaction(() => stats.Apply(UserStatsDelta.Single(UserStatType.FlagsSet, 10)));
        await RunTransaction(() => grain.Evaluate());

        await RunTransaction(() => stats.Apply(UserStatsDelta.Single(UserStatType.FlagsSet, 1)));
        await RunTransaction(() => grain.Evaluate());

        IReadOnlyList<InGameAchievementEntry>? unlocked = null;
        await RunTransaction(async () => {
            unlocked = await grain.GetUnlocked();
        });

        unlocked!.Count(e => e.Type == InGameAchievementType.FlagsSet).Should().Be(1);
    }

    [Fact]
    public async Task GetRewardOptions_AfterUnlock_IsFixedAndNotClaimed()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<IUserInGameAchievements>(id);
        var stats = GetGrain<IUserStats>(id);

        await RunTransaction(() => stats.Apply(UserStatsDelta.Single(UserStatType.FlagsSet, 10)));
        await RunTransaction(() => grain.Evaluate());

        IReadOnlyList<CardType>? first = null;
        IReadOnlyList<CardType>? second = null;

        await RunTransaction(async () => {
            first = await grain.GetRewardOptions(InGameAchievementType.FlagsSet, 1);
        });

        await RunTransaction(async () => {
            second = await grain.GetRewardOptions(InGameAchievementType.FlagsSet, 1);
        });

        first!.Should().NotBeEmpty();
        second.Should().Equal(first);

        IReadOnlyList<InGameAchievementEntry>? unlocked = null;
        await RunTransaction(async () => {
            unlocked = await grain.GetUnlocked();
        });

        unlocked!.Single(e => e.Type == InGameAchievementType.FlagsSet).Claimed.Should().BeFalse();
    }

    [Fact]
    public async Task ClaimReward_OfferedCard_GrantsCardAndClosesAchievement()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<IUserInGameAchievements>(id);
        var stats = GetGrain<IUserStats>(id);
        var cards = GetGrain<IUserCards>(id);

        await RunTransaction(() => stats.Apply(UserStatsDelta.Single(UserStatType.FlagsSet, 10)));
        await RunTransaction(() => grain.Evaluate());

        IReadOnlyList<CardType>? options = null;
        await RunTransaction(async () => {
            options = await grain.GetRewardOptions(InGameAchievementType.FlagsSet, 1);
        });

        var picked = options!.First();
        var claimed = false;

        await RunTransaction(async () => {
            claimed = await grain.ClaimReward(InGameAchievementType.FlagsSet, 1, picked);
        });

        claimed.Should().BeTrue();

        IReadOnlyList<CardType>? owned = null;
        IReadOnlyList<InGameAchievementEntry>? unlocked = null;

        await RunTransaction(async () => {
            owned = await cards.GetAll();
            unlocked = await grain.GetUnlocked();
        });

        owned!.Should().Contain(picked);

        var entry = unlocked!.Single(e => e.Type == InGameAchievementType.FlagsSet);
        entry.Claimed.Should().BeTrue();
        entry.UnlockedCard.Should().Be(picked);
    }

    [Fact]
    public async Task ClaimReward_CardOutsideOptions_IsRejected()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<IUserInGameAchievements>(id);
        var stats = GetGrain<IUserStats>(id);

        await RunTransaction(() => stats.Apply(UserStatsDelta.Single(UserStatType.FlagsSet, 10)));
        await RunTransaction(() => grain.Evaluate());

        IReadOnlyList<CardType>? options = null;
        await RunTransaction(async () => {
            options = await grain.GetRewardOptions(InGameAchievementType.FlagsSet, 1);
        });

        var foreign = CardTypeExtensions.All.First(card => options!.Contains(card) == false);
        var claimed = true;

        await RunTransaction(async () => {
            claimed = await grain.ClaimReward(InGameAchievementType.FlagsSet, 1, foreign);
        });

        claimed.Should().BeFalse();
    }

    [Fact]
    public async Task Reset_ClearsUnlocked()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<IUserInGameAchievements>(id);
        var stats = GetGrain<IUserStats>(id);

        await RunTransaction(() => stats.Apply(UserStatsDelta.Single(UserStatType.FlagsSet, 10)));
        await RunTransaction(() => grain.Evaluate());
        await RunTransaction(() => grain.Reset());

        IReadOnlyList<InGameAchievementEntry>? unlocked = null;
        await RunTransaction(async () => {
            unlocked = await grain.GetUnlocked();
        });

        unlocked!.Should().BeEmpty();
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
            entry.Cards.Should().BeEquivalentTo(GetSiloService<IUserDeckConfig>().Value.BaseDeck);
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
        selected.Should().BeEquivalentTo(GetSiloService<IUserDeckConfig>().Value.BaseDeck);
    }

    [Fact]
    public async Task Update_SingleDeck_PersistsCards()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<IUserDeck>(id);
        await RunTransaction(() => grain.Initialize());

        var cardsGrain = GetGrain<IUserCards>(id);
        await RunTransaction(() => cardsGrain.Initialize());
        await RunTransaction(() => cardsGrain.AddCard(CardType.Smoke));
        await RunTransaction(() => cardsGrain.AddCard(CardType.OpponentBomb));

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

        var cardsGrain = GetGrain<IUserCards>(id);
        await RunTransaction(() => cardsGrain.Initialize());
        await RunTransaction(() => cardsGrain.AddCard(CardType.Smoke));

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

        var cardsGrain = GetGrain<IUserCards>(id);
        await RunTransaction(() => cardsGrain.Initialize());
        await RunTransaction(() => cardsGrain.AddCard(CardType.Smoke));

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
    public async Task Initialize_IsIdempotent()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<IUserDeck>(id);
        await RunTransaction(() => grain.Initialize());

        var cardsGrain = GetGrain<IUserCards>(id);
        await RunTransaction(() => cardsGrain.Initialize());
        await RunTransaction(() => cardsGrain.AddCard(CardType.Smoke));

        var customCards = new List<CardType>
            { CardType.Smoke, CardType.Smoke, CardType.Smoke, CardType.Smoke, CardType.Smoke, CardType.Smoke };
        await RunTransaction(() => grain.Update(0, customCards));
        await RunTransaction(() => grain.Initialize());
        UserDeckState? state = null;

        await RunTransaction(async () => {
            state = await grain.GetState();
        });
        state!.Entries[0].Cards.Should().BeEquivalentTo(customCards);
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
        state.ParticipantDecks[user1].Should().BeEquivalentTo(GetSiloService<IUserDeckConfig>().Value.BaseDeck);
        state.ParticipantDecks[user2].Should().BeEquivalentTo(GetSiloService<IUserDeckConfig>().Value.BaseDeck);
    }

    [Fact]
    public async Task OnComplete_SetsWinnerAndDuration()
    {
        var (user1, user2) = await SetupUsers();
        var matchId = Guid.NewGuid();
        var match = GetGrain<IMatch>(matchId);
        await RunTransaction(() => match.Setup(GameMatchType.Single, new List<Guid> { user1, user2 }));
        await RunTransaction(() => match.OnComplete(user1, new Dictionary<Guid, UserStatsDelta>()));
        MatchState? state = null;

        await RunTransaction(async () => {
            state = await match.GetState();
        });
        state!.Winner.Should().Be(user1);
        state.Time.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task OnComplete_UpdatesWinnerRating()
    {
        var (user1, user2) = await SetupUsers();
        var matchId = Guid.NewGuid();
        var match = GetGrain<IMatch>(matchId);
        await RunTransaction(() => match.Setup(GameMatchType.Single, new List<Guid> { user1, user2 }));
        await RunTransaction(() => match.OnComplete(user1, new Dictionary<Guid, UserStatsDelta>()));
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
        await RunTransaction(() => match.OnComplete(user1, new Dictionary<Guid, UserStatsDelta>()));
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
        await RunTransaction(() => match.OnComplete(user1, new Dictionary<Guid, UserStatsDelta>()));
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
        await RunTransaction(() => match.OnComplete(user1, new Dictionary<Guid, UserStatsDelta>()));
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