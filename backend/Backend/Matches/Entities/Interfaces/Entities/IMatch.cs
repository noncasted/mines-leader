using Shared;

namespace Backend.Matches;

public interface IMatch : IGrainWithGuidKey
{
    [Transaction(TransactionOption.Join)]
    Task Setup(GameMatchType type, IReadOnlyList<Guid> participants);

    [Transaction(TransactionOption.Join)]
    Task OnComplete(Guid winnerId);
}