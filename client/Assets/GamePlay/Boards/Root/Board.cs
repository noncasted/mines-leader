using System.Collections.Generic;
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
        private readonly ViewableProperty<BoardState> _state = new(new BoardState());

        private IUpdater _updater;
        private IBoardActions _actions;
        private bool _isMine;

        public IBoardConstructionData ConstructionDataData => _constructionData;
        public IViewableProperty<BoardState> State => _state;
        public IReadOnlyDictionary<Vector2Int, IBoardCell> Cells => _cellsDictionary;
        public bool IsMine => _isMine;

        [Inject]
        private void Construct(
            IUpdater updater,
            IBoardActions actions)
        {
            _updater = updater;
            _actions = actions;
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

        public void Setup(bool isMine)
        {
            _isMine = isMine;

            foreach (var cell in _cells)
                cell.Setup(_updater, _actions);
        }

        public void UpdateState(int mines, int flags)
        {
            _state.Set(new BoardState { Mines = mines, Flags = flags });
        }

        public void Construct(CellView[] cells, BoardConstructionData constructionData)
        {
            _cells = cells;
            _constructionData = constructionData;
        }
    }
}
