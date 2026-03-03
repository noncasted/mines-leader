using Common.Reactive;

namespace Game.GamePlay;

public interface IGameRound
{
    IViewableProperty<IPlayer> CurrentPlayer { get; }

    Task<Guid> Process(IReadOnlyLifetime lifetime);
    void SkipTurn();
}