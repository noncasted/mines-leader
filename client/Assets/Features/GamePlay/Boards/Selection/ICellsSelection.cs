using Internal;

namespace GamePlay.Boards
{
    public interface ICellsSelection
    {
        IViewableProperty<IBoardCell> Selected { get; }

        void Start(IReadOnlyLifetime lifetime);
    }
}