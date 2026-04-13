using GamePlay.Loop;
using Internal;

namespace GamePlay.Players
{
    public interface IPlayerMoves
    {
        IViewableProperty<bool> IsTurn { get; }
        IViewableProperty<int> Current { get; }
        IViewableProperty<int> Max { get; }

        void Set(int left, int max, bool isAvailable);
    }

    public class PlayerMoves : IPlayerMoves
    {
        private readonly ViewableProperty<bool> _isTurn = new(false);
        private readonly ViewableProperty<int> _current = new();
        private readonly ViewableProperty<int> _max = new();

        public IViewableProperty<bool> IsTurn => _isTurn;

        public IViewableProperty<int> Current => _current;
        public IViewableProperty<int> Max => _max;

        public void Set(int left, int max, bool isAvailable)
        {
            _current.Set(left);
            _max.Set(max);
            _isTurn.Set(isAvailable);
        }
    }

    public static class PlayerTurnsExtensions
    {
        public static bool IsAvailable(this IPlayerMoves moves, IGameContext gameContext)
        {
            return gameContext.IsGameStarted && moves.IsTurn.Value == true && moves.Current.Value > 0;
        }
    }
}