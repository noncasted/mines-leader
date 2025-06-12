namespace Infrastructure.Messaging;

public interface IMessagingHub : IGrainWithGuidKey
{
    Task BindClient(MessagingObserverOverview overview, IMessagingObserver observer);
    Task Send(IMessageOptions options, IClusterMessage message);
    Task<TResponse> Send<TResponse>(IMessageOptions options, IClusterMessage message)
        where TResponse : IClusterMessage;
}