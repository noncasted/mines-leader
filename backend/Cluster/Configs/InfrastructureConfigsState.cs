using Infrastructure;
using Infrastructure.Execution;

namespace Cluster.Configs;

public class SideEffectsConfigState(IOrleans orleans, IMessaging messaging)
    : AddressableState<SideEffectsOptions>(orleans, messaging), ISideEffectsConfig;

public class MessageQueueConfigState(IOrleans orleans, IMessaging messaging)
    : AddressableState<MessageQueueOptions>(orleans, messaging), IMessageQueueConfig;

public class TaskBalancerConfigState(IOrleans orleans, IMessaging messaging)
    : AddressableState<TaskBalancerOptions>(orleans, messaging), ITaskBalancerConfig;
