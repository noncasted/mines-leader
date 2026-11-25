using GamePlay.Loop;
using GamePlay.Services;
using Internal;
using UnityEngine;

namespace GamePlay.Boards
{
    public interface ICellsSelection
    {
        IViewableProperty<IBoardCell> Selected { get; }

        void Start(IReadOnlyLifetime lifetime);
    }

    public class CellsSelection : ICellsSelection, IUpdatable
    {
        public CellsSelection(IUpdater updater, IGameInput input, IGameContext gameContext)
        {
            _updater = updater;
            _input = input;
            _gameContext = gameContext;
        }

        private readonly IUpdater _updater;
        private readonly IGameInput _input;
        private readonly IGameContext _gameContext;

        private readonly ViewableProperty<IBoardCell> _value = new();

        public IViewableProperty<IBoardCell> Selected => _value;

        public void Start(IReadOnlyLifetime lifetime)
        {
            _updater.Add(lifetime, this);
        }

        public void OnUpdate(float delta)
        {
            if (_gameContext.IsPaused == true)
                return;

            var target = FindSelection(_input.World);

            if (target == _value.Value)
                return;

            _value.Set(target);
        }

        // The cell under the pointer comes from its coordinates on each board, not from a check of every cell.
        private IBoardCell FindSelection(Vector2 world)
        {
            var players = _gameContext.All;

            for (var i = 0; i < players.Count; i++)
            {
                if (players[i].Board.TryGetCellAt(world, out var cell) == true)
                    return cell;
            }

            return null;
        }
    }
}
