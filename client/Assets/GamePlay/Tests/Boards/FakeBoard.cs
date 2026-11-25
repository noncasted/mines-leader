using System.Collections.Generic;
using GamePlay.Boards;
using Internal;
using Shared;
using UnityEngine;

namespace GamePlay.Tests
{
    // A board of FakeCells laid out like BoardConstructor builds one: cell (x, y) at firstCell + (x, y) * cellSize in the
    // world, no offset between cells. The board of a match is 16 x 16 cells of size 1 centred on its transform
    // (Board_Base.prefab), see Match(). Its transform is a shift: the layout of BoardConstructionData placed so that
    // cell (0, 0) lands on firstCell; MoveTo moves the board with its cells.
    public class FakeBoard : IBoard
    {
        public FakeBoard(Vector2Int size, float cellSize, Vector2 firstCell)
        {
            var data = new BoardConstructionData(size, cellSize);
            ConstructionDataData = data;
            _origin = firstCell - data.GetLocalPosition(Vector2Int.zero);

            for (var x = 0; x < size.x; x++)
            {
                for (var y = 0; y < size.y; y++)
                {
                    var position = new Vector2Int(x, y);
                    _cells.Add(position, new FakeCell(this, position));
                }
            }
        }

        private Vector2 _origin;

        private readonly Dictionary<Vector2Int, IBoardCell> _cells = new();
        private readonly ViewableProperty<BoardState> _state = new(new BoardState());
        private readonly ViewableProperty<bool> _isGenerated = new(false);

        public IBoardConstructionData ConstructionDataData { get; }
        public IViewableProperty<BoardState> State => _state;
        public IReadOnlyDictionary<Vector2Int, IBoardCell> Cells => _cells;
        public bool IsMine { get; private set; }
        public IViewableProperty<bool> IsGenerated => _isGenerated;

        // The board of a match with its centre at center.
        public static FakeBoard Match(Vector2 center)
        {
            return new FakeBoard(new Vector2Int(16, 16), 1f, center - new Vector2(7.5f, 7.5f));
        }

        public Vector2 WorldToLocal(Vector2 world)
        {
            return world - _origin;
        }

        public Vector2 LocalToWorld(Vector2 local)
        {
            return local + _origin;
        }

        public void MoveTo(Vector2 center)
        {
            _origin = center;
        }

        public FakeCell Cell(int x, int y)
        {
            return (FakeCell)_cells[new Vector2Int(x, y)];
        }

        public void Free(params Vector2Int[] positions)
        {
            foreach (var position in positions)
                _cells[position].EnsureFree();
        }

        public void Setup(bool isMine)
        {
            IsMine = isMine;
            _isGenerated.Set(false);
        }

        public void OnGenerated()
        {
            _isGenerated.Set(true);
        }

        public void UpdateState(int mines, int flags)
        {
            _state.Set(new BoardState { Mines = mines, Flags = flags });
        }
    }
}
