namespace Infrastructure.StorableActions;

public interface IBatchWriter<T> : IGrainWithStringKey
{
    Task Start();
    
    [Transaction(TransactionOption.Join)]
    Task Write(T value);
    
    Task Loop();
}