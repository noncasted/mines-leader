using Internal;

namespace GamePlay.Players
{
    public interface IPlayerResource
    {
        IViewableProperty<int> Current { get; }
        IViewableProperty<int> BaseMax { get; }
        IViewableProperty<int> ResultMax { get; }
    }
}