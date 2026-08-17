using GamePlay.Loop;
using Internal;

namespace GamePlay.Players
{
    public interface IPlayerMoves
    {
        IViewableProperty<bool> IsTurn { get; }
        IViewableProperty<int> Current { get; }
        IViewableProperty<int> BaseMax { get; }
        IViewableProperty<int> ResultMax { get; }

        void Set(int left, int baseMax, int resultMax, bool isAvailable);
    }

    public class PlayerMoves : IPlayerMoves
    {
        private readonly ViewableProperty<bool> _isTurn = new(false);
        private readonly ViewableProperty<int> _current = new();
        private readonly ViewableProperty<int> _baseMax = new();
        private readonly ViewableProperty<int> _resultMax = new();

        public IViewableProperty<bool> IsTurn => _isTurn;

        public IViewableProperty<int> Current => _current;
        public IViewableProperty<int> BaseMax => _baseMax;
        public IViewableProperty<int> ResultMax => _resultMax;

        public void Set(int left, int baseMax, int resultMax, bool isAvailable)
        {
            _current.Set(left);
            _baseMax.Set(baseMax);
            _resultMax.Set(resultMax);
            _isTurn.Set(isAvailable);
        }
    }

    public static class PlayerTurnsExtensions
    {
        public static bool IsAvailable(this IPlayerMoves moves, IGameContext gameContext)
        {
            return moves.CanSpend(gameContext, 1);
        }

        public static bool CanSpend(this IPlayerMoves moves, IGameContext gameContext, int cost)
        {
            return gameContext.IsGameStarted && moves.IsTurn.Value == true && moves.Current.Value >= cost;
        }
    }
}
