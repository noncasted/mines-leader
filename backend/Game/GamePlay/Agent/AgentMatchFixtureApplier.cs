using Game.GamePlay.Boards;
using Shared;

namespace Game.GamePlay;

public static class AgentMatchFixtureApplier
{
    public static void ApplyDecks(IGameContext context, AgentMatchFixture fixture, MoveSnapshot snapshot)
    {
        if (fixture.SelfDeck != null && fixture.SelfDeck.Count > 0)
        {
            var human = FindHuman(context);
            human.Deck.Replace(fixture.SelfDeck);
            snapshot.RecordDeckUpdate(human);
        }

        if (fixture.BotDeck != null && fixture.BotDeck.Count > 0)
        {
            var bot = FindBot(context);
            bot.Deck.Replace(fixture.BotDeck);
            snapshot.RecordDeckUpdate(bot);
        }
    }

    public static void Apply(IGameContext context, AgentMatchFixture fixture, MoveSnapshot snapshot)
    {
        var human = FindHuman(context);

        if (string.IsNullOrWhiteSpace(fixture.SelfBoardLayout) == false)
        {
            BoardLayoutParser.Apply(human.Board, fixture.SelfBoardLayout);
            RecordBoard(human.Board, snapshot);
        }

        if (fixture.SelfHand != null && fixture.SelfHand.Count > 0)
            ReplaceHand(human, fixture.SelfHand, snapshot);

        if (fixture.Mana.HasValue)
            human.Mana.SetCurrent(snapshot, fixture.Mana.Value);

        if (fixture.Moves.HasValue)
            human.Moves.SetCurrent(snapshot, fixture.Moves.Value);
    }

    public static bool ShouldSkipRestore(IPlayer player, AgentMatchFixture? fixture)
    {
        if (fixture == null)
            return false;

        return player.User.IsBot == false && fixture.SelfHand != null && fixture.SelfHand.Count > 0;
    }

    private static IPlayer FindHuman(IGameContext context)
    {
        foreach (var player in context.Players)
        {
            if (player.User.IsBot == false)
                return player;
        }

        throw new InvalidOperationException("Agent fixture requires a non-bot player");
    }

    private static IPlayer FindBot(IGameContext context)
    {
        foreach (var player in context.Players)
        {
            if (player.User.IsBot)
                return player;
        }

        throw new InvalidOperationException("Agent fixture bot deck requires a bot player");
    }

    private static void ReplaceHand(IPlayer player, IReadOnlyList<CardType> types, MoveSnapshot snapshot)
    {
        var existing = player.Hand.Entries.ToList();

        foreach (var card in existing)
        {
            player.Hand.Remove(card.Id);
            snapshot.RecordCardRemove(player.User.Id, card.Id);
        }

        foreach (var type in types)
        {
            var added = player.Hand.Add(type);
            snapshot.RecordCardAdd(player.User.Id, added.Id, added.Type);
        }
    }

    private static void RecordBoard(IBoard board, MoveSnapshot snapshot)
    {
        var size = board.Size.x;

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var position = new Position(x, y);
                if (board.Cells.TryGetValue(position, out var cell) == false)
                    continue;

                if (cell.Status == CellStatus.Free)
                {
                    snapshot.RecordCellFree(board, position);
                    snapshot.RecordMines(board, position, cell.AsFree().MinesAround);
                    continue;
                }

                snapshot.RecordCellTaken(board, position);

                if (cell.AsTaken().IsFlagged)
                    snapshot.RecordFlag(board, position, true);
            }
        }

        snapshot.RecordBoardStateUpdate(board);
    }
}
