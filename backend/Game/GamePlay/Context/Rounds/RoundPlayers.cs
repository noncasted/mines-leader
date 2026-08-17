namespace Game.GamePlay;

public class RoundPlayers
{
    public RoundPlayers(IGameContext gameContext)
    {
        _gameContext = gameContext;
    }

    private readonly IGameContext _gameContext;

    public void RestoreCards(IPlayer player, MoveSnapshot snapshot)
    {
        var cardsNeeded = player.Hand.Size - player.Hand.Entries.Count;
        var stashConsumed = false;
        var deckTouched = cardsNeeded > 0;

        for (var i = 0; i < cardsNeeded; i++)
        {
            if (player.Deck.Count == 0)
            {
                var stashCards = player.Stash.Collect();
                stashConsumed |= stashCards.Count > 0;

                foreach (var cardType in stashCards)
                    player.Deck.AddCard(cardType);
            }

            var card = player.Deck.DrawCard();
            var activeCard = player.Hand.Add(card);
            snapshot.RecordCardAdd(player.User.Id, activeCard.Id, activeCard.Type);
        }

        if (deckTouched == true)
            snapshot.RecordDeckUpdate(player);

        if (stashConsumed == true)
            snapshot.RecordStashUpdate(player);
    }


    public Guid GetFlagWinner()
    {
        foreach (var (player, board) in _gameContext.Boards)
        {
            var allMinesFlagged = true;

            if (board.Cells.Count == 0)
                continue;

            // Подорванная мина исчезает с поля, поэтому отметить её флагом уже нельзя.
            // Без этой проверки поле можно было бы "зачистить", просто взорвав оставшиеся мины.
            if (board.DetonatedMines > 0)
                continue;

            foreach (var (_, cell) in board.Cells)
            {
                if (cell.Status == CellStatus.Free)
                    continue;

                var taken = cell.ToTaken();

                if (taken.HasMine == true && taken.IsFlagged == false)
                {
                    allMinesFlagged = false;
                    break;
                }

                if (taken.HasMine == false && taken.IsFlagged == true)
                {
                    allMinesFlagged = false;
                    break;
                }
            }

            if (allMinesFlagged == true)
                return player.User.Id;
        }

        return Guid.Empty;
    }
}