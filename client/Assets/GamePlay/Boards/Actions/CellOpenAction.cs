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
            IGameFlow gameFlow,
            IGameContext gameContext,
            IGameInput input,
            ICellsSelection selection,
            IBoardGenerator boardGenerator,
            IBoardRevealer boardRevealer)
        {
            _gameFlow = gameFlow;
            _gameContext = gameContext;
            _input = input;
            _selection = selection;
            _boardGenerator = boardGenerator;
            _boardRevealer = boardRevealer;
        }

        private readonly IGameFlow _gameFlow;
        private readonly IGameContext _gameContext;
        private readonly IGameInput _input;
        private readonly ICellsSelection _selection;
        private readonly IBoardGenerator _boardGenerator;
        private readonly IBoardRevealer _boardRevealer;

        public void Start(IReadOnlyLifetime lifetime)
        {
            _input.Open.AdviseTrue(lifetime, TrySwitchMark);
        }

        private void TrySwitchMark()
        {
            if (_gameContext.Self.Turns.IsAvailable() == false)
                return;

            var cell = _selection.Selected.Value;

            if (cell == null)
                return;

            if (cell.Source.IsMine == false)
                return;

            if (cell.State.Value.Status != CellStatus.Taken)
                return;

            if (_gameContext.IsFirstOpened == false)
            {
                _gameContext.OnFirstOpen();
                var position = cell.BoardPosition;
                _boardGenerator.Generate(position);
            }

            var state = cell.EnsureTaken();

            if (state.IsFlagged.Value == true)
                return;

            _gameContext.Self.Turns.OnUsed();

            var isExploded = state.Open();

            if (isExploded == true)
            {
                _gameFlow.OnLose(_gameContext.Self);
            }
            else
            {
                if (_gameContext.Self.Board.HasMinesAround(cell.BoardPosition) == false)
                    _boardRevealer.Reveal(cell.BoardPosition);

                _gameContext.Self.Board.InvokeUpdated();
            }
        }
    }
}