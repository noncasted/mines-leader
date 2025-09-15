namespace Infrastructure.Messaging;

public interface IMessageStream : IGrainWithStringKey
{
    Task BindObserver(IMessageStreamObserver observer);
    Task Send(IClusterMessage message);
    Task<TResponse> Send<TResponse>(IClusterMessage message) where TResponse : IClusterMessage;
}