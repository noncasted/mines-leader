using Common.Network;
using Internal;
using Shared;

namespace GamePlay.Players
{
    public interface IPlayerMoves
    {
        IViewableProperty<bool> IsTurn { get; }
        IViewableProperty<int> Current { get; }
        IViewableProperty<int> Max { get; }
    }

    public class PlayerMoves : IPlayerMoves, IScopeLoaded
    {
        public PlayerMoves(NetworkProperty<PlayerMovesState> state)
        {
            _state = state;
        }

        private readonly NetworkProperty<PlayerMovesState> _state;

        private readonly ViewableProperty<bool> _isTurn = new(false);
        private readonly ViewableProperty<int> _current = new();
        private readonly ViewableProperty<int> _max = new();

        public IViewableProperty<bool> IsTurn => _isTurn;

        public IViewableProperty<int> Current => _current;
        public IViewableProperty<int> Max => _max;

        public void OnLoaded(IReadOnlyLifetime lifetime)
        {
            _state.View(lifetime, state =>
                {
                    _current.Set(state.Left);
                    _max.Set(state.Max);
                }
            );
        }
    }

    public static class PlayerTurnsExtensions
    {
        public static bool IsAvailable(this IPlayerMoves moves)
        {
            return moves.IsTurn.Value == true && moves.Current.Value > 0;
        }
    }
}