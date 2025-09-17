using Common;

namespace Infrastructure.Messaging;

public interface IMessageQueueClient
{
    IViewableDelegate<T> GetOrCreateConsumer<T>(IMessageQueueId id) where T : IClusterMessage;
    Task PushTransactional(IMessageQueueId id, IClusterMessage message);
    Task PushDirect(IMessageQueueId id, IClusterMessage message);
}