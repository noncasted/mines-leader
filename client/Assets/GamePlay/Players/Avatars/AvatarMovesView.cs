using System.Collections.Generic;
using GamePlay.Prefabs;
using Internal;
using Network;
using Tools.PrefabBuilder;
using UnityEngine;
using VContainer;

namespace GamePlay.Players
{
    [DisallowMultipleComponent]
    public class AvatarMovesView : MonoBehaviour, IScopeLoaded
    {
        [SerializeField] private float _spaceBetweenPoints = 0.1f;

        private readonly List<AvatarTurnPointView> _points = new();

        private IPlayerMoves _moves;
        private INetworkEntity _playerEntity;
        private GamePrefabs _prefabs;

        [Inject]
        private void Construct(IPlayerMoves moves, INetworkEntity playerEntity, GamePrefabs prefabs)
        {
            _prefabs = prefabs;
            _playerEntity = playerEntity;
            _moves = moves;
        }

        public void OnLoaded(IReadOnlyLifetime lifetime)
        {
            var viewPosition = transform.localPosition;

            if (_playerEntity.Owner.IsLocal == false)
                viewPosition.y *= -1f;

            transform.localPosition = viewPosition;

            _moves.Current.View(lifetime, Recalculate);
            _moves.BaseMax.View(lifetime, Recalculate);
            _moves.ResultMax.View(lifetime, Recalculate);

            void Recalculate()
            {
                var resultMax = _moves.ResultMax.Value;
                var current = _moves.Current.Value;
                var baseMax = _moves.BaseMax.Value;

                CheckObjects();
                SetPointTypes();
                AdjustPosition();
                SwitchPoints();

                void CheckObjects()
                {
                    if (_points.Count > resultMax)
                    {
                        var delta = _points.Count - resultMax;

                        for (var i = 0; i < delta; i++)
                        {
                            var point = _points[_points.Count - 1];
                            Destroy(point.gameObject);
                            _points.RemoveAt(_points.Count - 1);
                        }
                    }
                    else if (_points.Count < resultMax)
                    {
                        var delta = resultMax - _points.Count;

                        for (var i = 0; i < delta; i++)
                        {
                            var point = Instantiate(_prefabs.AvatarTurnPoint, transform);
                            _points.Add(point);
                        }
                    }
                }

                void SetPointTypes()
                {
                    for (var i = 0; i < _points.Count; i++)
                    {
                        var point = _points[i];

                        if (i < baseMax)
                            point.SetBase();
                        else
                            point.SetAdditional();
                    }
                }

                void AdjustPosition()
                {
                    var count = _points.Count;
                    var startX = -count / 2f * _spaceBetweenPoints + _spaceBetweenPoints / 2f;

                    for (var i = 0; i < count; i++)
                    {
                        var point = _points[i];
                        var x = startX + i * _spaceBetweenPoints;
                        var position = new Vector3(x, 0f, 0f);
                        point.transform.localPosition = position;

                        point.Hide();
                    }
                }

                void SwitchPoints()
                {
                    foreach (var point in _points)
                        point.Hide();

                    for (var i = 0; i < _points.Count; i++)
                    {
                        var point = _points[i];
                        var isActive = i < current;

                        if (isActive == true)
                            point.Show();
                        else
                            point.Hide();
                    }
                }
            }
        }
    }
}
