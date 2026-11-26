using Infrastructure;
using Infrastructure.Execution;

namespace Cluster.Configs;

public class DurableQueueConfigState(AddressableStateUtils utils)
    : AddressableState<DurableQueueOptions>(utils), IDurableQueueConfig;
