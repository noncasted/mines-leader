using Internal;

namespace GamePlay.Players
{
    public interface IPlayerTurns
    {
        IViewableProperty<bool> IsTurn { get; }
        IViewableProperty<int> Current { get; }
        IViewableProperty<int> Max { get; }

        IViewableDelegate Start { get; }
        IViewableDelegate End { get; }

        void SetCurrent(int amount);
        void SetMax(int amount);
        void OnUsed();
    }

    public static class PlayerTurnsExtensions
    {
        public static bool IsAvailable(this IPlayerTurns turns)
        {
            return turns.IsTurn.Value == true && turns.Current.Value > 0;
        }

        public static void AddCurrent(this IPlayerTurns turns, int amount)
        {
            turns.SetCurrent(turns.Current.Value + amount);
        }

        public static void RemoveCurrent(this IPlayerTurns turns, int amount)
        {
            turns.SetCurrent(turns.Current.Value - amount);
        }
    }
}