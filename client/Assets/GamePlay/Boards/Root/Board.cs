using System.Collections.Generic;
using Common.Network;
using Global.Systems;
using Internal;
using Shared;
using UnityEngine;
using VContainer;

namespace GamePlay.Boards
{
    [DisallowMultipleComponent]
    public class Board : MonoBehaviour, IBoard, IScopeSetup, IEntityComponent
    {
        [SerializeField] private CellView[] _cells;
        [SerializeField] private BoardConstructionData _constructionData;

        private readonly Dictionary<Vector2Int, IBoardCell> _cellsDictionary = new();

        private INetworkEntity _entity;
        private IUpdater _updater;
        private INetworkConnection _connection;
        private NetworkProperty<BoardState> _state;

        public IBoardConstructionData ConstructionDataData => _constructionData;
        public IViewableProperty<BoardState> State => _state;
        public IReadOnlyDictionary<Vector2Int, IBoardCell> Cells => _cellsDictionary;
        public bool IsMine => _entity.Owner.IsLocal;

        [Inject]
        private void Construct(
            IUpdater updater,
            INetworkEntity entity,
            NetworkProperty<BoardState> state,
            INetworkConnection connection)
        {
            _state = state;
            _connection = connection;
            _updater = updater;
            _entity = entity;
        }

        public void Register(IEntityBuilder builder)
        {
            builder.RegisterComponent(this)
                   .As<IBoard>()
                   .As<IScopeSetup>();
        }

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _cellsDictionary.Clear();

            foreach (var cell in _cells)
                _cellsDictionary.Add(cell.BoardPosition, cell);
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