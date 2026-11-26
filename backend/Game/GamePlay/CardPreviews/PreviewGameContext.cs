using Internal;
using Game.Session;

namespace Game.GamePlay.CardPreviews;

internal sealed class PreviewGameContext : IGameContext
{
    public PreviewGameContext(IPlayer self, IPlayer opponent)
    {
        _players = new List<IPlayer> { self, opponent };

        _boards = new Dictionary<IPlayer, IBoard>
        {
            [self] = self.Board,
            [opponent] = opponent.Board
        };

        _userToPlayer = new Dictionary<IUser, IPlayer>
        {
            [self.User] = self,
            [opponent.User] = opponent
        };
    }

    private readonly List<IPlayer> _players;
    private readonly Dictionary<IPlayer, IBoard> _boards;
    private readonly Dictionary<IUser, IPlayer> _userToPlayer;
    private readonly ViewableDelegate _gameStarted = new();

    public IReadOnlyList<IPlayer> Players => _players;
    public IReadOnlyDictionary<IPlayer, IBoard> Boards => _boards;
    public IReadOnlyDictionary<IUser, IPlayer> UserToPlayer => _userToPlayer;
    public IViewableDelegate GameStarted => _gameStarted;

    public void AddPlayer(IPlayer player) => throw new NotSupportedException();
    public void OnGameStarted() => throw new NotSupportedException();
}
