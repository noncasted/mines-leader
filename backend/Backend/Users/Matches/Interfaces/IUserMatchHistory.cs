using Backend.Matches;

namespace Backend.Users;

public interface IUserMatchHistory : IUserGrain
{
     [Transaction(TransactionOption.Join)]
     Task Add(MatchOverview match);
}