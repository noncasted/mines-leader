using System.Collections.Generic;
using Global.Systems;
using Internal;
using Shared;
using UnityEngine;
using VContainer;

namespace GamePlay.Boards
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoardConstructor))]
    public class Board : MonoBehaviour, IBoard, IScopeSetup, IEntityComponent
    {
        private readonly Dictionary<Vector2Int, IBoardCell> _cellsDictionary = new();
        private readonly ViewableProperty<BoardState> _state = new(new BoardState());

        private BoardConstructor _constructor;
        private CellView[] _cells;
        private IUpdater _updater;
        private IBoardActions _actions;
        private bool _isMine;

        private BoardConstructor Constructor
        {
            get
            {
                if (_constructor == null)
                    _constructor = GetComponent<BoardConstructor>();

                return _constructor;
            }
        }

        public IBoardConstructionData ConstructionDataData => Constructor.ConstructionData;
        public IViewableProperty<BoardState> State => _state;
        public IReadOnlyDictionary<Vector2Int, IBoardCell> Cells => _cellsDictionary;
        public bool IsMine => _isMine;

        [Inject]
        internal void Construct(
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
            _cells = Constructor.Build();

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
    }
}
