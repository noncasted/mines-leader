using GamePlay.Loop;
using GamePlay.Services;
using Internal;

namespace GamePlay.Boards
{
    public interface ICellFlagAction
    {
        void Start(IReadOnlyLifetime lifetime);
    }
    
    public class CellFlagAction : ICellFlagAction
    {
        public CellFlagAction(IGameInput input, ICellsSelection selection, IGameContext gameContext)
        {
            _input = input;
            _selection = selection;
            _gameContext = gameContext;
        }

        private readonly IGameInput _input;
        private readonly ICellsSelection _selection;
        private readonly IGameContext _gameContext;

        public void Start(IReadOnlyLifetime lifetime)
        {
            _input.Flag.AdviseTrue(lifetime, Perform);
        }

        private void Perform()
        {
            var own = _selection.Selected.Value;

            if (own == null)
                return;
            
            if (own.Source.IsMine == false)
                return;

            if (own.State.Value.Status != CellStatus.Taken)
                return;

            var state = own.EnsureTaken();

            if (state.IsFlagged.Value == false)
                state.Flag();
            else
                state.UnFlag();
            
            _gameContext.Self.Board.InvokeUpdated();
        }
    }
}