using Cluster.Deploy;
using Cluster.Lobbies;
using Cluster.Monitoring;
using Cluster.State;
using FluentAssertions;
using Framework.Tests.Fixtures;
using Infrastructure;
using Infrastructure.Diagnostics;
using Orleans.Serialization;
using Xunit;

namespace Framework.Tests.Messaging;

// Messages between services and grains: every value survives the serializer of a remote call and the copier of a local one.
[Collection(nameof(OrleansIntegrationCollection))]
public class OrleansMessagesSerializationTests(OrleansTestClusterFixture fixture)
    : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    private static readonly Guid SomeId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTime SomeTime = new(2026, 11, 2, 10, 20, 30, DateTimeKind.Utc);

    [Fact]
    public void DeployIdResponse_KeepsValues()
    {
        Check(new DeployIdResponse { DeployId = SomeId });
    }

    [Fact]
    public void OpenLobbies_KeepValues()
    {
        var lobbies = new OpenLobbies { Version = 5 };

        lobbies.Entries.Add(new OpenLobby
        {
            Code = "ABCD",
            ServerId = SomeId,
            Listing = new LobbyListing { IsOpen = true, PlayersCount = 2 }
        });

        Check(lobbies);
    }

    [Fact]
    public void SideEffectsSnapshotResponse_KeepsValues()
    {
        Check(new SideEffectsSnapshotResponse
        {
            ServiceTag = "tag",
            ServiceId = SomeId,
            Data = new SideEffectsLiveData
            {
                QueueCount = 1,
                ProcessingCount = 2,
                RetryCount = 3,
                DeadLetterCount = 4,
                ThroughputHistory =
                [
                    new SideEffectsThroughputEntry { Timestamp = SomeTime, Processed = 5, Failed = 6 }
                ],
                RetryEntries =
                [
                    new SideEffectsRetryEntry
                    {
                        Id = SomeId,
                        TypeName = "type",
                        RetryCount = 7,
                        RetryAfter = SomeTime,
                        CreatedAt = SomeTime
                    }
                ]
            }
        });
    }

    [Fact]
    public void ClusterFeaturesPatch_KeepsValues()
    {
        Check(new ClusterFeaturesPatch
        {
            AcceptingConnections = true,
            SideEffectsEnabled = false,
            CoordinatorUnhealthy = true,
            GameFlags = new Dictionary<string, bool> { ["flag"] = true }
        });
    }

    [Fact]
    public void HeapSnapshotRequest_KeepsValues()
    {
        Check(new HeapSnapshotRequest { Deep = true });
    }

    [Fact]
    public void HeapSnapshotResponse_KeepsValues()
    {
        var response = new HeapSnapshotResponse
        {
            ServiceName = "service",
            ServiceId = SomeId,
            Timestamp = SomeTime,
            WorkingSetBytes = 1,
            Deep = true
        };

        response.SetGcMemory(2, GC.GetGCMemoryInfo().GenerationInfo);
        response.SetTopTypes([new HeapTypeEntry { TypeName = "type", Count = 8, TotalBytes = 9 }]);
        response.SetError("error");
        response.SetCollectDuration(10);

        Check(response);
    }

    [Fact]
    public void SequencedMessage_KeepsValues()
    {
        Check(new SequencedMessage
        {
            Sequence = 1,
            Payload = new TestMessage { Text = "hello", Sequence = 2 },
            Epoch = SomeId
        });
    }

    [Fact]
    public void CatchUpResult_KeepsValues()
    {
        Check(new CatchUpResult
        {
            Messages = [new SequencedMessage { Sequence = 1, Payload = "payload", Epoch = SomeId }],
            GapDetected = true,
            CurrentSequence = 2,
            Epoch = SomeId
        });
    }

    [Fact]
    public void DurableQueueSideEffect_KeepsValues()
    {
        Check(new DurableQueueSideEffect
        {
            QueueName = "queue",
            Message = new TestMessage { Text = "hello" },
            CorrelationId = SomeId
        });
    }

    [Fact]
    public void TransactionContext_KeepsItsId()
    {
        Check(new TransactionContext { Id = SomeId });
    }

    private void Check<T>(T value) where T : class
    {
        var serializer = GetSiloService<Serializer>();
        var copier = GetSiloService<DeepCopier>();

        serializer.Deserialize<T>(serializer.SerializeToArray(value)).Should()
                  .BeEquivalentTo(value, options => options.PreferringRuntimeMemberTypes());
        copier.Copy(value).Should().BeEquivalentTo(value, options => options.PreferringRuntimeMemberTypes());
    }
}
