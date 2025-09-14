namespace Infrastructure.Messaging;

public interface IMessageStream : IGrainWithStringKey
{
    Task BindObserver(IMessageStreamObserver observer);
    Task Send(IClusterMessage message);
    Task<TResponse> SendStream<TResponse>(IMessageOptions options, IClusterMessage message)
        where TResponse : IClusterMessage;
}