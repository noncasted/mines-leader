using System.Collections.Generic;
using GamePlay.Loop;
using GamePlay.Services;
using Global.Systems;
using Internal;
using UnityEngine;

namespace GamePlay.Boards
{
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
            IBoardCell target = null;

            foreach (var player in _gameContext.All)
            {
                target = TryFindSelection(player.Board.Cells);

                if (target != null)
                    break;
            }

            if (target == _value.Value)
                return;
            
            _value.Set(target);
        }

        private IBoardCell TryFindSelection(IReadOnlyDictionary<Vector2Int, IBoardCell> cells)
        {
            foreach (var cell in cells.Values)
            {
                if (cell.PointerHandler.IsInside(_input.World))
                    return cell;
            }

            return null;
        }
    }
}