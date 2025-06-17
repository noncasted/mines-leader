using System.Collections.Generic;
using Common.Network;
using Common.Network.Common;
using Internal;
using UnityEngine;
using VContainer;

namespace GamePlay.Boards
{
    [DisallowMultipleComponent]
    public class Board : MonoBehaviour, IBoard, IScopeSetup
    {
        [SerializeField] private CellView[] _cells;
        [SerializeField] private BoardConstructionData _constructionData;

        private readonly ViewableDelegate _updated = new();
        private readonly Dictionary<Vector2Int, IBoardCell> _cellsDictionary = new();

        private INetworkEntity _entity;
        private NetworkProperty<NetworkBoardCellsState> _cellsState;

        public IBoardConstructionData ConstructionDataData => _constructionData;
        public IReadOnlyDictionary<Vector2Int, IBoardCell> Cells => _cellsDictionary;
        public bool IsMine => _entity.Owner.IsLocal;
        public IViewableDelegate Updated => _updated;

        [Inject]
        private void Construct(INetworkEntity entity, NetworkProperty<NetworkBoardCellsState> cellsState)
        {
            _entity = entity;
            _cellsState = cellsState;
        }

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _cellsDictionary.Clear();
            var rawCells = new Dictionary<Vector2Int, CellView>();

            foreach (var cell in _cells)
            {
                _cellsDictionary.Add(cell.BoardPosition, cell);
                rawCells.Add(cell.BoardPosition, cell);
            }

            _cellsState.Advise(lifetime, value =>
            {
                foreach (var (position, cell) in value.Cells)
                    rawCells[position].UpdateState(cell);
                
                _updated.Invoke();
            });
        }

        public void Setup(INetworkEntity entity)
        {
            foreach (var cell in _cells)
                cell.Setup();

            if (IsMine == true)
            {
                var state = new NetworkBoardCellsState()
                {
                    Cells = new Dictionary<Vector2Int, INetworkCellState>()
                };
                
                foreach (var cell in _cells)
                    state.Cells.Add(cell.BoardPosition, cell.State.Value.ToNetwork());

                _cellsState.Set(state);
            }
        }

        public void InvokeUpdated()
        {
            foreach (var cell in _cells)
                _cellsState.Value.Cells[cell.BoardPosition] = cell.State.Value.ToNetwork();
            
            _cellsState.MarkDirty();
            _updated.Invoke();
        }

        public void Construct(CellView[] cells, BoardConstructionData constructionData)
        {
            _cells = cells;
            _constructionData = constructionData;
        }
    }
}