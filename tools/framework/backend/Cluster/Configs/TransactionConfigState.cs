using Infrastructure;
using Infrastructure.Execution;

namespace Cluster.Configs;

public class TransactionConfigState(AddressableStateUtils utils)
    : AddressableState<TransactionOptions>(utils), ITransactionConfig;
