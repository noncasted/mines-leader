using Infrastructure;
using Infrastructure.Execution;

namespace Cluster.Configs;

public class SideEffectsConfigState(IOrleans orleans, IMessaging messaging)
    : AddressableState<SideEffectsOptions>(orleans, messaging), ISideEffectsConfig;

public class DurableQueueConfigState(IOrleans orleans, IMessaging messaging)
    : AddressableState<DurableQueueOptions>(orleans, messaging), IDurableQueueConfig;

public class TaskBalancerConfigState(IOrleans orleans, IMessaging messaging)
    : AddressableState<TaskBalancerOptions>(orleans, messaging), ITaskBalancerConfig;
