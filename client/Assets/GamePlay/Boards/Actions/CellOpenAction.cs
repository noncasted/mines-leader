using GamePlay.Loop;
using GamePlay.Players;
using GamePlay.Services;
using Internal;

namespace GamePlay.Boards
{
    public interface ICellOpenAction
    {
        void Start(IReadOnlyLifetime lifetime);
    }

    public class CellOpenAction : ICellOpenAction
    {
        public CellOpenAction(
            IGameCamera camera,
            IGameContext gameContext,
            IGameInput input,
            ICellsSelection selection)
        {
            _camera = camera;
            _gameContext = gameContext;
            _input = input;
            _selection = selection;
        }

        private readonly IGameCamera _camera;
        private readonly IGameContext _gameContext;
        private readonly IGameInput _input;
        private readonly ICellsSelection _selection;

        public void Start(IReadOnlyLifetime lifetime)
        {
            _input.Open.AdviseTrue(lifetime, Perform);
        }

        private void Perform()
        {
            if (_gameContext.Self.Moves.IsAvailable() == false)
                return;

            var cell = _selection.Selected.Value;

            if (cell == null)
                return;

            if (cell.Source.IsMine == false)
                return;

            if (cell.State.Value.Status != CellStatus.Taken)
                return;

            var state = cell.EnsureTaken();

            if (state.IsFlagged.Value == true)
                return;

            state.Open();
        }
    }
}