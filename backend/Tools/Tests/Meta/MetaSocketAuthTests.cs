using FluentAssertions;
using Infrastructure;
using Meta.Users;
using MetaGateway.UserFlow.Connection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shared;
using Tests.Fixtures;
using Xunit;

namespace Tests.Meta;

// S3: every way into the meta socket against the real user grains. Before S3 the bare user id in the query of the
// socket was the login: whoever knew a Guid (the console, the peers of a match) entered that account. "Old" users are
// made as the server before S3 made them: registered, without a guest secret; there is no migration window.
[Collection(nameof(OrleansIntegrationCollection))]
public class MetaSocketAuthTests(OrleansTestClusterFixture fixture)
    : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    [Fact]
    public async Task NewGuest_GetsUserAndSecret()
    {
        var result = await Auth().Authenticate(new SharedBackendSocketAuth.Request());

        result.Response.Status.Should().Be(SharedBackendSocketAuth.AuthStatus.Ok);
        result.Response.UserId.Should().NotBe(Guid.Empty);
        result.Response.IssuedSecret.Should().NotBeNullOrEmpty();
        result.User!.Id.Should().Be(result.Response.UserId);
        (await Overview(result.Response.UserId)).HasGuestSecret.Should().BeTrue();
    }

    [Fact]
    public async Task GuestWithSecret_EntersTheSameUser_NoNewSecret()
    {
        var auth = Auth();
        var created = (await auth.Authenticate(new SharedBackendSocketAuth.Request())).Response;

        var again = (await auth.Authenticate(Guest(created.UserId, created.IssuedSecret))).Response;

        again.Status.Should().Be(SharedBackendSocketAuth.AuthStatus.Ok);
        again.UserId.Should().Be(created.UserId);
        again.IssuedSecret.Should().BeEmpty();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GuestWithWrongSecret_IsRefused(bool wellFormed)
    {
        var auth = Auth();
        var created = (await auth.Authenticate(new SharedBackendSocketAuth.Request())).Response;
        var other = (await auth.Authenticate(new SharedBackendSocketAuth.Request())).Response;
        var secret = wellFormed == true ? other.IssuedSecret : "not a secret";

        var result = await auth.Authenticate(Guest(created.UserId, secret));

        result.Response.Status.Should().Be(SharedBackendSocketAuth.AuthStatus.InvalidCredentials);
        result.Response.UserId.Should().BeEmpty();
        result.User.Should().BeNull();
    }

    // A wiped database (a local reset): the saved guest is unknown, the client gets a new one.
    [Fact]
    public async Task UnknownGuest_GetsNewGuest()
    {
        var unknown = Guid.NewGuid();
        var (secret, _) = GuestSecrets.Generate();

        var response = (await Auth().Authenticate(Guest(unknown, secret))).Response;

        response.Status.Should().Be(SharedBackendSocketAuth.AuthStatus.Ok);
        response.UserId.Should().NotBe(unknown);
        response.IssuedSecret.Should().NotBeEmpty();
    }

    // A user from before S3 has no secret: its id with any secret is refused, no secret is issued to it, it is not
    // replaced by a new guest either (no migration window, decision of 2026-10-21).
    [Fact]
    public async Task UserWithoutSecret_IsRefused_StaysWithoutSecret()
    {
        var old = await CreateUserWithoutSecret();
        var (secret, _) = GuestSecrets.Generate();

        var result = await Auth().Authenticate(Guest(old, secret));

        result.Response.Status.Should().Be(SharedBackendSocketAuth.AuthStatus.InvalidCredentials);
        result.User.Should().BeNull();
        (await Overview(old)).HasGuestSecret.Should().BeFalse();
    }

    // A bare id (what the query of a client before S3 carried) is not a way in.
    [Fact]
    public async Task IdWithoutSecret_IsInvalidRequest()
    {
        var old = await CreateUserWithoutSecret();

        var response = (await Auth().Authenticate(new SharedBackendSocketAuth.Request { GuestId = old })).Response;

        response.Status.Should().Be(SharedBackendSocketAuth.AuthStatus.InvalidRequest);
        response.UserId.Should().BeEmpty();
    }

    [Fact]
    public async Task SecretWithoutId_IsInvalidRequest()
    {
        var (secret, _) = GuestSecrets.Generate();

        var response = (await Auth().Authenticate(new SharedBackendSocketAuth.Request { GuestSecret = secret })).Response;

        response.Status.Should().Be(SharedBackendSocketAuth.AuthStatus.InvalidRequest);
    }

    private MetaSocketAuth Auth()
    {
        var projections = Substitute.For<IUserProjectionsLoader>();
        projections.Load(Arg.Any<Guid>()).Returns(Task.FromResult<IReadOnlyList<IProjectionPayload>>([]));

        var factory = new UserFactory(GetSiloService<IOrleans>(), projections, NullLogger<UserFactory>.Instance);
        return new MetaSocketAuth(factory, NullLogger<MetaSocketAuth>.Instance);
    }

    private static SharedBackendSocketAuth.Request Guest(Guid id, string secret)
    {
        return new SharedBackendSocketAuth.Request { GuestId = id, GuestSecret = secret };
    }

    // A user as the server before S3 created it: registered, no secret.
    private async Task<Guid> CreateUserWithoutSecret()
    {
        var id = Guid.NewGuid();
        var orleans = GetSiloService<IOrleans>();

        await RunTransaction(async () => {
            var handle = orleans.CreateUserHandle(id);
            await handle.Entity.Initialize();
            await handle.Auth.OnRegistered();
        });

        return id;
    }

    private async Task<UserAuthOverview> Overview(Guid id)
    {
        UserAuthOverview? overview = null;

        await RunTransaction(async () => {
            overview = await GetGrain<IUserAuth>(id).GetOverview();
        });

        return overview!;
    }
}
