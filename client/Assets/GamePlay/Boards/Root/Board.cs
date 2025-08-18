using System.Collections.Generic;
using Common.Network;
using Global.Backend;
using Global.Systems;
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
        private IUpdater _updater;
        private INetworkConnection _connection;

        public IBoardConstructionData ConstructionDataData => _constructionData;
        public IReadOnlyDictionary<Vector2Int, IBoardCell> Cells => _cellsDictionary;
        public bool IsMine => _entity.Owner.IsLocal;
        public IViewableDelegate Updated => _updated;

        [Inject]
        private void Construct(IUpdater updater, INetworkEntity entity, INetworkConnection connection)
        {
            _connection = connection;
            _updater = updater;
            _entity = entity;
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
        }

        public void Setup(INetworkEntity entity)
        {
            foreach (var cell in _cells)
                cell.Setup(_updater, _connection);
        }

        public void Construct(CellView[] cells, BoardConstructionData constructionData)
        {
            _cells = cells;
            _constructionData = constructionData;
        }
    }
}