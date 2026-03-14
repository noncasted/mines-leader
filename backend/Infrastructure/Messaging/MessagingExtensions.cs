using Common.Extensions;
using Common.Reactive;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Infrastructure;

public interface IMessageQueueId
{
    string ToRaw();
}

public class MessageQueueId : IMessageQueueId
{
    public MessageQueueId(string id)
    {
        _id = id;
    }

    private readonly string _id;

    public string ToRaw()
    {
        return _id;
    }
}

public interface IMessagePipeId
{
    string ToRaw();
}

public class MessagePipeId : IMessagePipeId
{
    public MessagePipeId(string id)
    {
        _id = id;
    }

    private readonly string _id;

    public string ToRaw()
    {
        return _id;
    }
}

public static class MessagingExtensions
{
    extension(IMessaging messaging)
    {
        public Task PushTransactionalQueue(IMessageQueueId id, object message)
        {
            return messaging.Queue.PushTransactional(id, message);
        }

        public Task PushDirectQueue(IMessageQueueId id, object message)
        {
            return messaging.Queue.PushDirect(id, message);
        }

        public async Task ListenQueue<T>(
            IReadOnlyLifetime lifetime,
            IMessageQueueId id,
            Action<T> listener)
        {
            var consumer = await messaging.Queue.GetOrCreateConsumer<T>(id);
            consumer.Advise(lifetime, listener);
        }

        public async Task ListenPipe<T>(
            IReadOnlyLifetime lifetime,
            IMessagePipeId id,
            Action<T> listener)
        {
            var consumer = await messaging.Pipe.CreateListener<T>(lifetime, id);
            consumer.Advise(lifetime, listener);
        }

        public Task AddPipeRequestHandler<TRequest, TResponse>(
            IReadOnlyLifetime lifetime,
            IMessagePipeId id,
            Func<TRequest, Task<TResponse>> listener)
        {
            return messaging.Pipe.AddHandler(lifetime, id, listener);
        }

        public Task SendPipe(IMessagePipeId id, object message)
        {
            return messaging.Pipe.Send(id, message);
        }

        public Task<TResponse> SendPipe<TResponse>(IMessagePipeId id, object message)
        {
            return messaging.Pipe.Send<TResponse>(id, message);
        }
    }

    public static IHostApplicationBuilder AddMessaging(this IHostApplicationBuilder builder)
    {
        builder.Add<Messaging>()
            .As<IMessaging>();

        builder.Add<MessageQueueClient>()
            .As<IMessageQueueClient>();

        builder.Add<MessagePipeClient>()
            .As<IMessagePipeClient>();

        return builder;
    }
}