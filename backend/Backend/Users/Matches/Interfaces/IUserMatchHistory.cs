using Backend.Matches;

namespace Backend.Users;

public interface IUserMatchHistory : IGrainWithGuidKey
{
     [Transaction(TransactionOption.Join)]
     Task Add(MatchOverview match);
}