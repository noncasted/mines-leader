using Internal;

namespace GamePlay.Players
{
    public interface IPlayerMana
    {
        IViewableProperty<int> Current { get; }
        IViewableProperty<int> BaseMax { get; }
        IViewableProperty<int> ResultMax { get; }

        void Set(int current, int baseMax, int resultMax);
    }

    public class PlayerMana : IPlayerMana
    {
        private readonly ViewableProperty<int> _current = new();
        private readonly ViewableProperty<int> _baseMax = new();
        private readonly ViewableProperty<int> _resultMax = new();

        public IViewableProperty<int> Current => _current;
        public IViewableProperty<int> BaseMax => _baseMax;
        public IViewableProperty<int> ResultMax => _resultMax;

        public void Set(int current, int baseMax, int resultMax)
        {
            _current.Set(current);
            _baseMax.Set(baseMax);
            _resultMax.Set(resultMax);
        }
    }
}