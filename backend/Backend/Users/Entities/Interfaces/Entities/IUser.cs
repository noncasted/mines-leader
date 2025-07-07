namespace Backend.Users;

public interface IUser : IUserGrain
{
    [Transaction(TransactionOption.Join)]
    Task Initialize(UserCreateOptions options);
    
    [Transaction(TransactionOption.Join)]
    Task SetName(string name);
}

[GenerateSerializer]
public class UserCreateOptions
{
    [Id(0)]
    public required string Name { get; init; }
}