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

        for (var i = 0; i < cardsNeeded; i++)
        {
            if (player.Deck.Count == 0)
            {
                var stashCards = player.Stash.Collect();

                foreach (var cardType in stashCards)
                    player.Deck.AddCard(cardType);
            }

            var card = player.Deck.DrawCard();
            var activeCard = player.Hand.Add(card);
            snapshot.RecordCardAdd(player.User.Id, activeCard.Id, activeCard.Type);
        }
    }


    public Guid GetFlagWinner()
    {
        foreach (var (player, board) in _gameContext.Boards)
        {
            var allMinesFlagged = true;

            if (board.Cells.Count == 0)
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
            }

            if (allMinesFlagged == true)
                return player.User.Id;
        }

        return Guid.Empty;
    }
}