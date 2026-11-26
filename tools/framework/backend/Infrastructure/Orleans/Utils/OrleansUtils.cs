using Infrastructure.State;
using Microsoft.Extensions.Logging;

namespace Infrastructure;

public interface IOrleans
{
    IClusterClient Client { get; }
    ITransactions Transactions { get; }
    IStateStorage StateStorage { get; }
    IStateSerializer Serializer { get; }
    ILogger Logger { get; }
}

public class OrleansUtils : IOrleans
{
    public OrleansUtils(
        IClusterClient client,
        IGrainFactory grains,
        ITransactions transactions,
        IStateSerializer serializer,
        ILogger<OrleansUtils> logger,
        IStateStorage stateStorage)
    {
        Grains = grains;
        Transactions = transactions;
        Logger = logger;
        StateStorage = stateStorage;
        Serializer = serializer;
        Client = client;
    }

    public IGrainFactory Grains { get; }
    public ITransactions Transactions { get; }

    public IClusterClient Client { get; }
    public IStateStorage StateStorage { get; }
    public IStateSerializer Serializer { get; }
    public ILogger Logger { get; }
}
