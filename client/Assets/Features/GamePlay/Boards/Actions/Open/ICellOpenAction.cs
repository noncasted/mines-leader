using Internal;

namespace GamePlay.Boards
{
    public interface ICellOpenAction
    {
        void Start(IReadOnlyLifetime lifetime);
    }
}