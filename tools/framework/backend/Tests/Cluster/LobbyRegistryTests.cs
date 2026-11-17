using Cluster.Discovery;
using Cluster.Lobbies;
using FluentAssertions;
using Framework.Tests.Fixtures;
using Shared;
using Xunit;

namespace Framework.Tests.Cluster;

// The registry grain itself: persistent state in the "cluster" table, one grain per deploy.
[Collection(nameof(OrleansIntegrationCollection))]
public class LobbyRegistryTests(OrleansTestClusterFixture fixture) : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    private const int MaxPlayers = 4;

    [Fact]
    public async Task Register_ReturnsValidCode_FindReturnsItsSession()
    {
        var registry = GetGrain<ILobbyRegistry>(Guid.NewGuid());
        var serverId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        var code = await registry.Register(serverId, sessionId);
        var entry = await registry.Find(code);

        LobbyCode.IsValid(code).Should().BeTrue();
        entry.Should().NotBeNull();
        entry!.ServerId.Should().Be(serverId);
        entry.SessionId.Should().Be(sessionId);
    }

    [Fact]
    public async Task Register_ManyLobbies_AllCodesAreDifferent()
    {
        var registry = GetGrain<ILobbyRegistry>(Guid.NewGuid());
        var codes = new HashSet<string>();

        for (var i = 0; i < 50; i++)
            codes.Add(await registry.Register(Guid.NewGuid(), Guid.NewGuid()));

        codes.Should().HaveCount(50);
    }

    [Fact]
    public async Task Find_UnknownCode_ReturnsNull()
    {
        var registry = GetGrain<ILobbyRegistry>(Guid.NewGuid());
        var code = await registry.Register(Guid.NewGuid(), Guid.NewGuid());
        var unknown = code == "ZZZZZZ" ? "YYYYYY" : "ZZZZZZ";

        (await registry.Find(unknown)).Should().BeNull();
    }

    [Fact]
    public async Task Release_RemovesCode_OtherCodesStay()
    {
        var registry = GetGrain<ILobbyRegistry>(Guid.NewGuid());
        var firstSession = Guid.NewGuid();
        var secondSession = Guid.NewGuid();

        var first = await registry.Register(Guid.NewGuid(), firstSession);
        var second = await registry.Register(Guid.NewGuid(), secondSession);

        await registry.Release(first, firstSession);

        (await registry.Find(first)).Should().BeNull();
        (await registry.Find(second))!.SessionId.Should().Be(secondSession);
    }

    [Fact]
    public async Task Release_ByAnotherSession_ThrowsAndKeepsCode()
    {
        var registry = GetGrain<ILobbyRegistry>(Guid.NewGuid());
        var sessionId = Guid.NewGuid();
        var code = await registry.Register(Guid.NewGuid(), sessionId);

        var release = () => registry.Release(code, Guid.NewGuid());

        await release.Should().ThrowAsync<Exception>();
        (await registry.Find(code))!.SessionId.Should().Be(sessionId);
    }

    [Fact]
    public async Task MarkStarted_EntryGetsTheMatch_CodeStaysTakenUntilRelease()
    {
        var registry = GetGrain<ILobbyRegistry>(Guid.NewGuid());
        var serverId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var matchId = Guid.NewGuid();

        var code = await registry.Register(serverId, sessionId);
        (await registry.Find(code))!.MatchSessionId.Should().Be(Guid.Empty);

        await registry.MarkStarted(code, sessionId, matchId);

        var entry = await registry.Find(code);

        entry.Should().NotBeNull();
        entry!.ServerId.Should().Be(serverId);
        entry.SessionId.Should().Be(sessionId);
        entry.MatchSessionId.Should().Be(matchId);

        await registry.Release(code, sessionId);

        (await registry.Find(code)).Should().BeNull();
    }

    [Fact]
    public async Task MarkStarted_ByAnotherSessionOrTwiceOrUnknownCode_Throws_EntryStays()
    {
        var registry = GetGrain<ILobbyRegistry>(Guid.NewGuid());
        var sessionId = Guid.NewGuid();
        var matchId = Guid.NewGuid();
        var code = await registry.Register(Guid.NewGuid(), sessionId);
        var unknown = code == "ZZZZZZ" ? "YYYYYY" : "ZZZZZZ";

        var byAnother = () => registry.MarkStarted(code, Guid.NewGuid(), Guid.NewGuid());
        var unknownCode = () => registry.MarkStarted(unknown, sessionId, Guid.NewGuid());
        var emptyMatch = () => registry.MarkStarted(code, sessionId, Guid.Empty);

        await byAnother.Should().ThrowAsync<Exception>();
        await unknownCode.Should().ThrowAsync<Exception>();
        await emptyMatch.Should().ThrowAsync<Exception>();
        (await registry.Find(code))!.MatchSessionId.Should().Be(Guid.Empty);

        await registry.MarkStarted(code, sessionId, matchId);

        var twice = () => registry.MarkStarted(code, sessionId, Guid.NewGuid());

        await twice.Should().ThrowAsync<Exception>();
        (await registry.Find(code))!.MatchSessionId.Should().Be(matchId);
    }

    [Fact]
    public async Task Registries_OfDifferentDeploys_DoNotShareCodes()
    {
        var first = GetGrain<ILobbyRegistry>(Guid.NewGuid());
        var second = GetGrain<ILobbyRegistry>(Guid.NewGuid());

        var code = await first.Register(Guid.NewGuid(), Guid.NewGuid());

        (await second.Find(code)).Should().BeNull();
    }

    [Fact]
    public async Task UpdateListing_OpenLobbyWithFreePlace_IsListed_ClosedOrFull_IsNot()
    {
        var deployId = Guid.NewGuid();
        var registry = GetGrain<ILobbyRegistry>(deployId);
        var serverId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var leader = Guid.NewGuid();
        await Announce(deployId, serverId);
        var code = await registry.Register(serverId, sessionId);

        (await registry.GetOpenLobbies()).Entries.Should().BeEmpty();

        await registry.UpdateListing(code, sessionId, Listing(leader, isOpen: true, players: 2));

        var open = await registry.GetOpenLobbies();
        open.Entries.Should().ContainSingle();
        open.Entries[0].Code.Should().Be(code);
        open.Entries[0].ServerId.Should().Be(serverId);
        open.Entries[0].Listing.LeaderUserId.Should().Be(leader);
        open.Entries[0].Listing.PlayersCount.Should().Be(2);
        open.Entries[0].Listing.MaxPlayers.Should().Be(MaxPlayers);

        await registry.UpdateListing(code, sessionId, Listing(leader, isOpen: true, players: MaxPlayers));
        (await registry.GetOpenLobbies()).Entries.Should().BeEmpty();

        await registry.UpdateListing(code, sessionId, Listing(leader, isOpen: false, players: 1));
        (await registry.GetOpenLobbies()).Entries.Should().BeEmpty();

        (await registry.GetOpenLobbies()).Version.Should().BeGreaterThan(open.Version);
    }

    [Fact]
    public async Task Release_ListedLobby_LeavesTheList_VersionGrows()
    {
        var deployId = Guid.NewGuid();
        var registry = GetGrain<ILobbyRegistry>(deployId);
        var serverId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        await Announce(deployId, serverId);
        var code = await registry.Register(serverId, sessionId);

        await registry.UpdateListing(code, sessionId, Listing(Guid.NewGuid(), isOpen: true, players: 1));
        var listed = await registry.GetOpenLobbies();
        listed.Entries.Should().ContainSingle();

        await registry.Release(code, sessionId);
        var released = await registry.GetOpenLobbies();

        released.Entries.Should().BeEmpty();
        released.Version.Should().BeGreaterThan(listed.Version);
    }

    [Fact]
    public async Task UpdateListing_ByAnotherSession_Throws()
    {
        var registry = GetGrain<ILobbyRegistry>(Guid.NewGuid());
        var code = await registry.Register(Guid.NewGuid(), Guid.NewGuid());

        var update = () => registry.UpdateListing(code, Guid.NewGuid(), Listing(Guid.NewGuid(), isOpen: true, players: 1));

        await update.Should().ThrowAsync<InvalidOperationException>();
    }

    // A lobby is listed only while its game server is alive in the discovery of the deploy.
    private Task Announce(Guid deployId, Guid serverId)
    {
        return GetGrain<IServiceDiscoveryStorage>(deployId).Update(new ServiceOverview
        {
            Id = serverId,
            Tag = TestRoles.Game,
            UpdateTime = DateTime.UtcNow
        });
    }

    private static LobbyListing Listing(Guid leader, bool isOpen, int players)
    {
        return new LobbyListing
        {
            IsOpen = isOpen,
            LeaderUserId = leader,
            PlayersCount = players,
            MaxPlayers = MaxPlayers
        };
    }

    [Fact]
    public void GenerateUnique_SkipsTakenCodes()
    {
        var generated = new Queue<string>(["AAAAAA", "BBBBBB", "CCCCCC"]);
        var taken = new HashSet<string> { "AAAAAA", "BBBBBB" };

        LobbyCodeGenerator.GenerateUnique(taken.Contains, generated.Dequeue).Should().Be("CCCCCC");
    }

    [Fact]
    public void GenerateUnique_EveryCodeIsTaken_Throws()
    {
        var generate = () => LobbyCodeGenerator.GenerateUnique(_ => true);

        generate.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Generate_ReturnsValidCodes()
    {
        for (var i = 0; i < 1000; i++)
            LobbyCode.IsValid(LobbyCodeGenerator.Generate()).Should().BeTrue();
    }

    // The listing rule the game servers report against: open, somebody in it, a free place, a leader.
    [Fact]
    public void Listing_IsListed_NeedsOpenPlayersFreePlaceAndLeader()
    {
        Listing(Guid.NewGuid(), isOpen: true, players: 1).IsListed.Should().BeTrue();
        Listing(Guid.NewGuid(), isOpen: false, players: 1).IsListed.Should().BeFalse();
        Listing(Guid.NewGuid(), isOpen: true, players: 0).IsListed.Should().BeFalse();
        Listing(Guid.NewGuid(), isOpen: true, players: MaxPlayers).IsListed.Should().BeFalse();
        Listing(Guid.Empty, isOpen: true, players: 1).IsListed.Should().BeFalse();

        // A listing without the places of its lobby is never shown.
        new LobbyListing { IsOpen = true, PlayersCount = 1, LeaderUserId = Guid.NewGuid() }
            .IsListed.Should().BeFalse();
    }
}
