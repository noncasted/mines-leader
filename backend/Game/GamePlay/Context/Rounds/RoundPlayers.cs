using Microsoft.Extensions.Options;
using Shared;

namespace Game.GamePlay;

public class RoundPlayers
{
    public RoundPlayers(IGameContext gameContext, IOptions<GameOptions> options)
    {
        _gameContext = gameContext;
        _options = options;
    }

    private readonly IGameContext _gameContext;
    private readonly IOptions<GameOptions> _options;
    
    public void Setup()
    {
        var options = _options.Value;

        foreach (var player in _gameContext.Players)
        {
            player.Hand.SetSize(options.HandSize);

            player.Health.SetMax(options.MaxHealth);
            player.Health.SetCurrent(options.MaxHealth);

            player.Mana.SetMax(options.StartMana);
            player.Mana.Restore();

            player.Moves.SetMax(options.MovesCount);
        }
    }
    
    public void RestoreCard(IPlayer player, MoveSnapshot snapshot) {
        var cardsNeeded = _options.Value.HandSize - player.Hand.Entries.Count;

        for (var i = 0; i < cardsNeeded; i++) {
            if (player.Deck.Count == 0) {
                var stashCards = player.Stash.Collect();

                foreach (var cardType in stashCards)
                    player.Deck.AddCard(cardType);
            }

            var card = player.Deck.DrawCard();
            player.Hand.Add(card);
            snapshot.RecordCardDraw(player.User.Id, card);
        }
    }

    public Guid GetFlagWinner() {
        foreach (var (player, board) in _gameContext.Boards) {
            var allMinesFlagged = true;

            foreach (var (_, cell) in board.Cells) {
                if (cell.Status == CellStatus.Free)
                    continue;

                var taken = cell.ToTaken();

                if (taken.HasMine == true && taken.IsFlagged == false) {
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