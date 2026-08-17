using Sirenix.OdinInspector;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GamePlay.Boards
{
    [DisallowMultipleComponent]
    public class BoardConstructor : MonoBehaviour
    {
        [SerializeField] private CellView _cellPrefab;
        [SerializeField] private Board _board;

        [SerializeField] private Vector2Int _size;
        [SerializeField] private float _cellSize;
        [SerializeField] private float _cellsOffset;

        private BoardConstructionData _constructionData;

        public BoardConstructionData ConstructionData =>
            _constructionData ??= new BoardConstructionData(_size, _cellSize);

        public CellView[] Build()
        {
            var origin = GetOrigin();
            var cells = new CellView[_size.x * _size.y];
            var index = 0;

            for (var x = 0; x < _size.x; x++)
            {
                for (var y = 0; y < _size.y; y++)
                {
                    var cell = Instantiate(_cellPrefab, transform);
                    var position = new Vector2Int(x, y);

                    cell.transform.localPosition = GetLocalPosition(position, origin);
                    cell.name = $"Cell_{x}_{y}";
                    cell.Construct(position, _board);

                    cells[index] = cell;
                    index++;
                }
            }

            return cells;
        }

        private Vector2 GetOrigin()
        {
            var origin = new Vector2(_size.x * _cellSize / 2, _size.y * _cellSize / 2) * -1f;

            if (_size.x % 2 == 0)
                origin.x += _cellSize / 2;

            if (_size.y % 2 == 0)
                origin.y += _cellSize / 2;

            return origin;
        }

        private Vector3 GetLocalPosition(Vector2Int position, Vector2 origin)
        {
            return new Vector3(
                position.x * _cellSize + origin.x + position.x * _cellsOffset,
                position.y * _cellSize + origin.y + position.y * _cellsOffset);
        }

        private void OnValidate()
        {
            if (_board == null)
                _board = GetComponent<Board>();

            _constructionData = null;
        }

#if UNITY_EDITOR
        [Button("Preview (editor only, clear before saving)")]
        private void Create()
        {
            Clear();

            var origin = GetOrigin();

            for (var x = 0; x < _size.x; x++)
            {
                for (var y = 0; y < _size.y; y++)
                {
                    var cell = (CellView)PrefabUtility.InstantiatePrefab(_cellPrefab, transform);
                    var position = new Vector2Int(x, y);

                    cell.transform.localPosition = GetLocalPosition(position, origin);
                    cell.name = $"Cell_{x}_{y}";
                    cell.Construct(position, _board);
                }
            }
        }

        [Button("Clear")]
        private void Clear()
        {
            var existingCells = GetComponentsInChildren<CellView>(true);

            foreach (var cell in existingCells)
                DestroyImmediate(cell.gameObject);
        }

        private void OnDrawGizmosSelected()
        {
            if (_size.x <= 0 || _size.y <= 0)
                return;

            var origin = GetOrigin();

            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = Color.yellow;

            for (var x = 0; x < _size.x; x++)
            {
                for (var y = 0; y < _size.y; y++)
                {
                    var center = GetLocalPosition(new Vector2Int(x, y), origin);
                    Gizmos.DrawWireCube(center, new Vector3(_cellSize, _cellSize, 0f));
                }
            }
        }
#endif
    }
}
