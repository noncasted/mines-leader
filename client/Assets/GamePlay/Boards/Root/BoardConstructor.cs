using Internal;
using UnityEngine;
#if UNITY_EDITOR
using System;
using UnityEditor;
#endif

namespace GamePlay.Boards
{
    [DisallowMultipleComponent]
    public class BoardConstructor : MonoBehaviour
    {
        [SerializeField] private Board _board;
        [SerializeField] private BoardCellKind _cellKind;

        [SerializeField] private Vector2Int _size;
        [SerializeField] private float _cellSize;

        private BoardConstructionData _constructionData;

        public BoardConstructionData ConstructionData =>
            _constructionData ??= new BoardConstructionData(_size, _cellSize);

        private static CellView GetCellPrefab(BoardCellKind kind)
        {
            return kind == BoardCellKind.Opponent
                ? GamePlayPrefabs.CellOpponent
                : GamePlayPrefabs.CellOwn;
        }

        public CellView[] Build()
        {
            var cellPrefab = GetCellPrefab(_cellKind);
            var cells = new CellView[_size.x * _size.y];
            var index = 0;

            for (var x = 0; x < _size.x; x++)
            {
                for (var y = 0; y < _size.y; y++)
                {
                    var cell = Instantiate(cellPrefab, transform);
                    var position = new Vector2Int(x, y);

                    cell.transform.localPosition = ConstructionData.GetLocalPosition(position);
                    cell.name = $"Cell_{x}_{y}";
                    cell.Construct(position, _board);

                    cells[index] = cell;
                    index++;
                }
            }

            return cells;
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

            var cellPrefab = LoadEditorCellPrefab(_cellKind);

            for (var x = 0; x < _size.x; x++)
            {
                for (var y = 0; y < _size.y; y++)
                {
                    var cell = (CellView)PrefabUtility.InstantiatePrefab(cellPrefab, transform);
                    var position = new Vector2Int(x, y);

                    cell.transform.localPosition = ConstructionData.GetLocalPosition(position);
                    cell.name = $"Cell_{x}_{y}";
                    cell.Construct(position, _board);
                }
            }
        }

        private static CellView LoadEditorCellPrefab(BoardCellKind kind)
        {
            if (GamePlayPrefabs.IsLoaded)
                return GetCellPrefab(kind);

            var asset = AssetDatabase.LoadAssetAtPath<PrefabGroupAsset>(
                "Assets/Common/Internal/Runtime/Catalogues/Prefabs/GamePlay.asset");
            if (asset == null)
                throw new InvalidOperationException(
                    "GamePlay prefab group asset is missing. Run Tools/GeneratePrefabsCatalog.");

            return asset.Get<CellView>(kind == BoardCellKind.Opponent ? "CellOpponent" : "CellOwn");
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

            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = Color.yellow;

            for (var x = 0; x < _size.x; x++)
            {
                for (var y = 0; y < _size.y; y++)
                {
                    var center = ConstructionData.GetLocalPosition(new Vector2Int(x, y));
                    Gizmos.DrawWireCube(center, new Vector3(_cellSize, _cellSize, 0f));
                }
            }
        }
#endif
    }
}
