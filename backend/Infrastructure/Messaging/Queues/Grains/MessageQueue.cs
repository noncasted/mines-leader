using Common;
using Infrastructure.StorableActions;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Messaging;

public class MessageQueue : BatchWriter<MessageQueueState, IClusterMessage>, IMessageQueue
{
    public MessageQueue(
        [States.MessageQueue] IPersistentState<MessageQueueState> state,
        ILogger<MessageQueue> logger) : base(state)
    {
        _state = state;
        _logger = logger;
    }

    private readonly List<IMessageQueueObserver> _observers = new();
    private readonly IPersistentState<MessageQueueState> _state;
    private readonly ILogger<MessageQueue> _logger;

    protected override BatchWriterOptions Options { get; } = new()
    {
        RequiresTransaction = false
    };

    public Task AddObserver(IMessageQueueObserver observer)
    {
        _observers.Add(observer);
        return Task.CompletedTask;
    }

    public Task PushDirect(IClusterMessage message)
    {
        return WriteDirect(message);
    }

    public Task PushTransactional(IClusterMessage message)
    {
        return WriteTransactional(message);
    }

    protected override async Task Process(IReadOnlyList<IClusterMessage> entries)
    {
        var toRemove = new List<IMessageQueueObserver>();

        await Task.WhenAll(_observers.Select(observer =>
                {
                    try
                    {
                        return observer.Send(entries);
                    }
                    catch (Exception e)
                    {
                        toRemove.Add(observer);
                        
                        _logger.LogError(e,
                            "[Messaging] [Queue] Delevering message from {QueueName} to observer failed",
                            StringId
                        );
                        return Task.CompletedTask;
                    }
                }
            )
        );
        
        foreach (var observer in toRemove)
            _observers.Remove(observer);
    }
}