using System.Diagnostics;
using Common.Extensions;
using Common.Network;
using FluentAssertions;
using Game.MatchMetrics;
using Internal;
using MemoryPack;
using MetaGateway.UserFlow.Commands;
using MetaGateway.UserFlow.Connection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shared;
using Xunit;

namespace Tests.Meta.Telemetry;

// The wiring of the game data of matches in this game: the user of the stream is the one of the session; the logic of
// the streams and the storage is tested in the framework.
public class MatchMetricsCommandsTests
{
    private readonly RecordingAdmission _admission = new();
    private readonly SocketSession _session = new();

    private IMatchMetricsConnections Connections()
    {
        var builder = WebApplication.CreateBuilder();
        builder.AddMatchMetricsCommands();
        builder.Services.AddSingleton<IMatchMetricsAdmission>(_admission);
        builder.Services.AddSingleton(Substitute.For<IDbSource>());

        return builder.Services.BuildServiceProvider().GetRequiredService<IMatchMetricsConnections>();
    }

    [Fact]
    public async Task Open_AsksTheAdmissionForTheUserOfTheSession()
    {
        IUserCommand command = new MatchMetricsCommands.Open(Connections());
        var match = Guid.NewGuid();

        var response = await command.Execute(_session, new SharedMatchMetrics.Open { MatchId = match, WriterId = Guid.NewGuid() });

        response.Should().BeOfType<SharedMatchMetrics.Opened>().Which.Status.Should().Be(SharedMatchMetrics.MatchMetricsStatus.NotInMatch);
        _admission.Asked.Should().Equal((match, _session.UserId));
    }

    [Fact]
    public async Task Batch_OfAStreamNotOpenedOnTheSocketIsRejected()
    {
        IUserCommand command = new MatchMetricsCommands.Batch(Connections());

        var response = await command.Execute(_session, new SharedMatchMetrics.Batch { StreamId = Guid.NewGuid(), Seq = 1 });

        response.Should().BeOfType<SharedMatchMetrics.BatchResult>().Which.Status.Should().Be(SharedMatchMetrics.MatchMetricsStatus.Rejected);
    }

    [Fact]
    public void Register_EveryRequestOfTheContractHasItsCommand()
    {
        var builder = WebApplication.CreateBuilder();
        builder.AddMatchMetricsCommands();
        builder.Services.AddSingleton<IMatchMetricsAdmission>(_admission);
        builder.Services.AddSingleton(Substitute.For<IDbSource>());

        var commands = builder.Services.BuildServiceProvider()
                              .GetServices<IUserCommand>()
                              .Select(command => command.RequestType);

        commands.Should().BeEquivalentTo([typeof(SharedMatchMetrics.Open), typeof(SharedMatchMetrics.Batch)]);
    }

    [Theory]
    [MemberData(nameof(Messages))]
    public void Union_KeepsTheTypeOfEveryMessage(INetworkContext message)
    {
        var bytes = MemoryPackSerializer.Serialize(message);

        MemoryPackSerializer.Deserialize<INetworkContext>(bytes).Should().BeOfType(message.GetType());
    }

    public static TheoryData<INetworkContext> Messages()
    {
        return new TheoryData<INetworkContext>
        {
            new SharedMatchMetrics.Open(),
            new SharedMatchMetrics.Opened(),
            new SharedMatchMetrics.Batch(),
            new SharedMatchMetrics.BatchResult()
        };
    }

    private class RecordingAdmission : IMatchMetricsAdmission
    {
        public List<(Guid, Guid)> Asked { get; } = new();

        public Task<MatchMetricsAdmit> Check(Guid matchId, Guid userId)
        {
            Asked.Add((matchId, userId));
            return Task.FromResult(new MatchMetricsAdmit(false, null));
        }
    }

    private class SocketSession : IUserSession
    {
        public IConnection Connection => throw new InvalidOperationException("The commands of the game data do not use the connection");
        public Guid UserId { get; } = Guid.NewGuid();
        public ActivityTraceId TraceId => default;
        public IReadOnlyLifetime Lifetime { get; } = new Lifetime();
    }
}
