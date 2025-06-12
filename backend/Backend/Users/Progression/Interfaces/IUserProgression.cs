namespace Backend.Users;

public interface IUserProgression : IGrainWithGuidKey
{
    [Transaction(TransactionOption.Join)]
    Task AddRecord(IUserProgressionRecord record);
}