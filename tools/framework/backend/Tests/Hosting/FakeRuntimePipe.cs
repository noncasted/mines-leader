using Cluster.Discovery;
using Common.Network;
using FluentAssertions;
using Framework.Tests.Fixtures;
using Framework.Tests.Cluster;
using Framework.Tests.Security;
using Framework.Tests.Session;
using Game.Hosting;
using Game.Lobbies;
using Game.Session;
using Infrastructure;
using Internal;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shared;
using Xunit;

namespace Framework.Tests.Hosting;

// The message pipes as a direct call: a handler added by its pipe id answers the send to the same id.
public class FakeRuntimePipe : IRuntimePipeClient
{
    private readonly Dictionary<string, Func<object, Task<object>>> _handlers = new();
    private readonly List<Type> _sent = new();

    // How many requests of the type went through the pipe.
    public int Count<TRequest>()
    {
        lock (_sent)
            return _sent.Count(type => type == typeof(TRequest));
    }

    public Task Start(IReadOnlyLifetime lifetime)
    {
        return Task.CompletedTask;
    }

    public Task<bool> Exists(IRuntimePipeId id)
    {
        lock (_handlers)
            return Task.FromResult(_handlers.ContainsKey(id.ToRaw()));
    }

    public Task AddHandler<TRequest, TResponse>(
        IReadOnlyLifetime lifetime,
        IRuntimePipeId id,
        Func<TRequest, Task<TResponse>> listener)
    {
        var raw = id.ToRaw();

        lock (_handlers)
            _handlers.Add(raw, async request => (await listener((TRequest)request))!);

        lifetime.Listen(() => {
            lock (_handlers)
                _handlers.Remove(raw);
        });

        return Task.CompletedTask;
    }

    public async Task<TResponse> Send<TResponse>(IRuntimePipeId id, object message)
    {
        Func<object, Task<object>>? handler;

        lock (_sent)
            _sent.Add(message.GetType());

        lock (_handlers)
        {
            if (_handlers.TryGetValue(id.ToRaw(), out handler) == false)
                throw new InvalidOperationException($"No handler of pipe {id.ToRaw()}");
        }

        return (TResponse)await handler(message);
    }
}
