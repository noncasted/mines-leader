using Internal;

namespace GamePlay.Players
{
    public interface IPlayerHealth
    {
        IViewableProperty<int> Current { get; }
        IViewableProperty<int> Max { get; }
        
        void SetCurrent(int amount);
        void SetMax(int amount);
    }
}