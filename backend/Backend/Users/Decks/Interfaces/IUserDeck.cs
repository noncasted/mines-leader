using Shared;

namespace Backend.Users;

public interface IUserDeck : IGrainWithGuidKey
{
    [Transaction(TransactionOption.Join)]
    Task Initialize();

    [Transaction(TransactionOption.Join)]
    Task Update(IReadOnlyDictionary<int, IReadOnlyList<CardType>> decks, int selectedIndex);
}