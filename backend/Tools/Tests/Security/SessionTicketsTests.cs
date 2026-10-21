using System.Buffers.Text;
using Common.Network;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Orchestration;
using Xunit;

namespace Tests.Security;

// S4: the tickets of the game gateway - HMAC-SHA256 "v1.payload.mac", 60 s, one use.
public class SessionTicketsTests
{
    [Fact]
    public void Issued_VerifiesWithItsUserAndSession()
    {
        var tickets = TestTickets.Create(new ManualTimeProvider());
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        var result = tickets.Verify(tickets.Issue(userId, sessionId));

        result.Check.Should().Be(SessionTicketCheck.Ok);
        result.Ticket.UserId.Should().Be(userId);
        result.Ticket.SessionId.Should().Be(sessionId);
    }

    [Fact]
    public void Format_IsVersionPayloadMac()
    {
        var ticket = TestTickets.Create().Issue(Guid.NewGuid(), Guid.NewGuid());
        var parts = ticket.Split('.');

        parts.Should().HaveCount(3);
        parts[0].Should().Be("v1");
        Base64Url.DecodeFromChars(parts[1]).Should().HaveCount(56);
        Base64Url.DecodeFromChars(parts[2]).Should().HaveCount(32);
    }

    [Fact]
    public void SecondUse_IsReplayed()
    {
        var tickets = TestTickets.Create(new ManualTimeProvider());
        var ticket = tickets.Issue(Guid.NewGuid(), Guid.NewGuid());

        tickets.Verify(ticket).Check.Should().Be(SessionTicketCheck.Ok);
        tickets.Verify(ticket).Check.Should().Be(SessionTicketCheck.Replayed);
    }

    [Fact]
    public void Expired_IsRefused_JustBeforeExpiry_IsAccepted()
    {
        var time = new ManualTimeProvider();
        var tickets = TestTickets.Create(time);
        var early = tickets.Issue(Guid.NewGuid(), Guid.NewGuid());
        var late = tickets.Issue(Guid.NewGuid(), Guid.NewGuid());

        time.Advance(SessionTickets.Lifetime - TimeSpan.FromSeconds(1));
        tickets.Verify(early).Check.Should().Be(SessionTicketCheck.Ok);

        time.Advance(TimeSpan.FromSeconds(1));
        tickets.Verify(late).Check.Should().Be(SessionTicketCheck.Expired);
    }

    [Fact]
    public void SignedWithAnotherKey_IsInvalid()
    {
        var other = new SessionTickets(new SessionTicketKey(new byte[32]), TimeProvider.System);
        var ticket = other.Issue(Guid.NewGuid(), Guid.NewGuid());

        TestTickets.Create().Verify(ticket).Check.Should().Be(SessionTicketCheck.Invalid);
    }

    // Another user or session written into a valid ticket: the mac no longer matches.
    [Fact]
    public void TamperedPayload_IsInvalid()
    {
        var tickets = TestTickets.Create();
        var parts = tickets.Issue(Guid.NewGuid(), Guid.NewGuid()).Split('.');
        var payload = Base64Url.DecodeFromChars(parts[1]);

        Guid.NewGuid().TryWriteBytes(payload.AsSpan(0, 16));
        var forged = $"{parts[0]}.{Base64Url.EncodeToString(payload)}.{parts[2]}";

        tickets.Verify(forged).Check.Should().Be(SessionTicketCheck.Invalid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("v1")]
    [InlineData("v1..")]
    [InlineData("v2.AAAA.AAAA")]
    [InlineData("v1.not base64.AAAA")]
    [InlineData("v1.AAAA.AAAA")]
    [InlineData("v1.AAAA.AAAA.AAAA")]
    public void Malformed_IsInvalid(string ticket)
    {
        TestTickets.Create().Verify(ticket).Check.Should().Be(SessionTicketCheck.Invalid);
    }

    [Fact]
    public void ReadKey_Development_EmptyGivesTheDevelopmentKey()
    {
        var key = SessionTicketsSetup.ReadKey(Configuration(null), Environment("Development"));

        key.Value.Should().Equal(SessionTicketsSetup.DevelopmentKey);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void ReadKey_OutsideDevelopment_EmptyThrows(string environment)
    {
        var read = () => SessionTicketsSetup.ReadKey(Configuration(null), Environment(environment));

        read.Should().Throw<InvalidOperationException>().WithMessage($"*{SessionTicketsSetup.SecretKey}*");
    }

    [Fact]
    public void ReadKey_OutsideDevelopment_DevelopmentKeyThrows()
    {
        var value = Convert.ToBase64String(SessionTicketsSetup.DevelopmentKey);
        var read = () => SessionTicketsSetup.ReadKey(Configuration(value), Environment("Production"));

        read.Should().Throw<InvalidOperationException>().WithMessage("*development key*");
    }

    [Theory]
    [InlineData("not base64!")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAA==")]
    public void ReadKey_NotBase64OrShort_Throws(string value)
    {
        var read = () => SessionTicketsSetup.ReadKey(Configuration(value), Environment("Production"));

        read.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ReadKey_Production_ValidKey()
    {
        var bytes = Enumerable.Range(0, 48).Select(i => (byte)(i * 7)).ToArray();

        var key = SessionTicketsSetup.ReadKey(Configuration(Convert.ToBase64String(bytes)), Environment("Production"));

        key.Value.Should().Equal(bytes);
    }

    private static IConfiguration Configuration(string? secret)
    {
        return new ConfigurationBuilder()
               .AddInMemoryCollection([new KeyValuePair<string, string?>(SessionTicketsSetup.SecretKey, secret)])
               .Build();
    }

    private static IHostEnvironment Environment(string name)
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(name);
        return environment;
    }
}
