using Internal;

namespace GamePlay.Cards
{
    public interface ICardPointerHandler
    {
        IViewableProperty<bool> IsHovered { get; }
        IViewableProperty<bool> IsPressed { get; }
    }
}