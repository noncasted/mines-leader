using Internal;

namespace GamePlay.Players
{
    public interface IPlayerHealth
    {
        IViewableProperty<int> Current { get; }
        IViewableProperty<int> Max { get; }

        void Set(int current, int max);
    }

    public class PlayerHealth : IPlayerHealth
    {
        private readonly ViewableProperty<int> _current = new();
        private readonly ViewableProperty<int> _max = new();

        public IViewableProperty<int> Current => _current;
        public IViewableProperty<int> Max => _max;

        public void Set(int current, int max)
        {
            _current.Set(current);
            _max.Set(max);
        }
    }
}