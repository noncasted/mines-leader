using GamePlay.Loop;
using GamePlay.Players;
using GamePlay.Services;
using Internal;
using UnityEngine;

namespace GamePlay.Boards
{
    public interface ICellMultipleOpenAction
    {
        void Start(IReadOnlyLifetime lifetime);
    }

    public class CellMultipleOpenAction : ICellMultipleOpenAction
    {
        public CellMultipleOpenAction(
            IBoardActions actions,
            IGameContext gameContext,
            IGameInput input,
            ICellsSelection selection)
        {
            _actions = actions;
            _gameContext = gameContext;
            _input = input;
            _selection = selection;
        }

        private const float DoubleClickTime = 0.3f;

        private readonly IBoardActions _actions;
        private readonly IGameContext _gameContext;
        private readonly IGameInput _input;
        private readonly ICellsSelection _selection;

        private float _lastClickTime;

        public void Start(IReadOnlyLifetime lifetime)
        {
            _input.Open.AdviseTrue(lifetime, Perform);
        }

        private void Perform()
        {
            var timeSinceLastClick = Time.time - _lastClickTime;

            if (timeSinceLastClick > DoubleClickTime)
            {
                _lastClickTime = Time.time;
                return;
            }

            _lastClickTime = 0f;

            if (_gameContext.Self.Moves.IsAvailable(_gameContext) == false)
                return;

            var cell = _selection.Selected.Value;

            if (cell == null)
                return;

            if (cell.Source.IsMine == false)
                return;

            if (cell.State.Value.Status == CellStatus.Taken)
                return;

            _actions.OpenMultiple(cell.BoardPosition);
        }
    }
}
