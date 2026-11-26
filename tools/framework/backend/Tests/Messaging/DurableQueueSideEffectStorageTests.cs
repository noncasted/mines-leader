using FluentAssertions;
using Infrastructure;
using Infrastructure.State;
using Newtonsoft.Json;
using Xunit;

namespace Framework.Tests.Messaging;

// A side effect waits in side_effects_queue as JSON: the rows written by an older build must still load, and MessageId
// must survive the load, a new one on a retry would deliver the message to the observers that already have it.
public class DurableQueueSideEffectStorageTests
{
    private const string StoredRow =
        "{\"$type\":\"Infrastructure.DurableQueueSideEffect, Infrastructure\"," +
        "\"QueueName\":\"queue\"," +
        "\"Message\":{\"$type\":\"Framework.Tests.Messaging.TestMessage, Framework.Tests\",\"Text\":\"hello\",\"Sequence\":7}," +
        "\"CorrelationId\":\"11111111-1111-1111-1111-111111111111\"," +
        "\"MessageId\":\"22222222-2222-2222-2222-222222222222\"}";

    private static readonly Guid CorrelationId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid MessageId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void StoredRow_Loads_WithEveryValue()
    {
        var effect = Load(StoredRow);

        effect.QueueName.Should().Be("queue");
        effect.CorrelationId.Should().Be(CorrelationId);
        effect.MessageId.Should().Be(MessageId);
        effect.Message.Should().BeEquivalentTo(new TestMessage { Text = "hello", Sequence = 7 });
    }

    [Fact]
    public void Write_KeepsTheFormatOfTheStoredRows()
    {
        var effect = Load(StoredRow);

        JsonConvert.SerializeObject(effect, typeof(ISideEffect), JsonStateSettings.CreateBase()).Should().Be(StoredRow);
    }

    [Fact]
    public void RoundTrip_KeepsTheGeneratedMessageId()
    {
        var effect = new DurableQueueSideEffect
        {
            QueueName = "queue",
            Message = new TestMessage { Text = "hello" },
            CorrelationId = CorrelationId
        };

        var json = JsonConvert.SerializeObject(effect, typeof(ISideEffect), JsonStateSettings.CreateBase());

        Load(json).MessageId.Should().Be(effect.MessageId);
    }

    private static DurableQueueSideEffect Load(string json)
    {
        var effect = JsonConvert.DeserializeObject<ISideEffect>(json, JsonStateSettings.CreateBase());

        return effect.Should().BeOfType<DurableQueueSideEffect>().Subject;
    }
}
