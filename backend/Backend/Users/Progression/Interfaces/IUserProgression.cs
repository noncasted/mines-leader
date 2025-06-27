namespace Backend.Users;

public interface IUserProgression : IUserGrain
{
    [Transaction(TransactionOption.Join)]
    Task AddRecord(IUserProgressionRecord record);
}