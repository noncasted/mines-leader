using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace GamePlay.Boards.Constructor
{
    [DisallowMultipleComponent]
    public class BoardConstructor : MonoBehaviour
    {
        [SerializeField] private CellView _cellPrefab;
        [SerializeField] private Board _board;

        [SerializeField] private Vector2Int _size;
        [SerializeField] private float _cellSize;
        [SerializeField] private float _cellsOffset;

        [Button]
        private void Create()
        {
            var offset = new Vector2(_size.x * _cellSize / 2, _size.y * _cellSize / 2) * -1f;

            if (_size.x % 2 == 0)
                offset.x += _cellSize / 2;

            if (_size.y % 2 == 0)
                offset.y += _cellSize / 2;

            var existingCells = GetComponentsInChildren<CellView>(true);

            foreach (var cell in existingCells)
                DestroyImmediate(cell.gameObject);

            var cells = new List<CellView>();

            for (var x = 0; x < _size.x; x++)
            {
                for (var y = 0; y < _size.y; y++)
                {
#if UNITY_EDITOR
                    var cell = PrefabUtility.InstantiatePrefab(_cellPrefab, transform) as CellView;
                    var position = new Vector3(
                        x * _cellSize + offset.x + x * _cellsOffset,
                        y * _cellSize + offset.y + y * _cellsOffset);

                    cell.transform.localPosition = position;
                    cell.name = $"Cell_{x}_{y}";
                    
                    var boardPosition = new Vector2Int(x, y);
                    cell.ConstructFromBuild(boardPosition, _board);

                    cells.Add(cell);
#endif
                }
            }

            _board.Construct(cells.ToArray(), new BoardConstructionData(_size, _cellSize));
        }

        private void OnValidate()
        {
            if (_board == null)
                _board = GetComponent<Board>();
        }
    }
}