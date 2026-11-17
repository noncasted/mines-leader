using Infrastructure;
using Infrastructure.Execution;

namespace Cluster.Configs;

public class TaskBalancerConfigState(AddressableStateUtils utils)
    : AddressableState<TaskBalancerOptions>(utils), ITaskBalancerConfig;
