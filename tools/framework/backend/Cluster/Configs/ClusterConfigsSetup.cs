using Common.Extensions;
using Internal;
using Infrastructure;
using Infrastructure.Execution;
using Microsoft.Extensions.Logging;

namespace Cluster.Configs;

public class ClusterConfigsSetup : ICoordinatorSetupCompleted
{
    public ClusterConfigsSetup(
        ISideEffectsConfig sideEffects,
        IDurableQueueConfig messageQueue,
        ITaskBalancerConfig taskBalancer,
        IRuntimePipeConfig runtimePipe,
        IRuntimeChannelConfig runtimeChannel,
        ITransactionConfig transactionConfig,
        ILogger<ClusterConfigsSetup> logger)
    {
        _sideEffects = sideEffects;
        _durableQueue = messageQueue;
        _taskBalancer = taskBalancer;
        _runtimePipe = runtimePipe;
        _runtimeChannel = runtimeChannel;
        _transactionConfig = transactionConfig;
        _logger = logger;
    }

    private readonly ISideEffectsConfig _sideEffects;
    private readonly IDurableQueueConfig _durableQueue;
    private readonly ITaskBalancerConfig _taskBalancer;
    private readonly IRuntimePipeConfig _runtimePipe;
    private readonly IRuntimeChannelConfig _runtimeChannel;
    private readonly ITransactionConfig _transactionConfig;
    private readonly ILogger<ClusterConfigsSetup> _logger;

    public async Task OnCoordinatorSetupCompleted(IReadOnlyLifetime lifetime)
    {
        await ConfigsInitializer.Init("config.sideEffects", _sideEffects, _logger);
        await ConfigsInitializer.Init("config.durableQueue", _durableQueue, _logger);
        await ConfigsInitializer.Init("config.taskBalancer", _taskBalancer, _logger);
        await ConfigsInitializer.Init("config.runtimePipe", _runtimePipe, _logger);
        await ConfigsInitializer.Init("config.runtimeChannel", _runtimeChannel, _logger);
        await ConfigsInitializer.Init("config.transaction", _transactionConfig, _logger);
    }
}
