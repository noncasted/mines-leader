using System.Collections.Generic;
using Common.Network;
using Internal;
using UnityEngine;
using VContainer;

namespace GamePlay.Players
{
    [DisallowMultipleComponent]
    public class AvatarTurnsView : MonoBehaviour, IScopeLoaded
    {
        [SerializeField] private float _spaceBetweenPoints = 0.1f;
        [SerializeField] private AvatarTurnPointView _pointPrefab;

        private readonly List<AvatarTurnPointView> _points = new();

        private IPlayerMoves _moves;
        private INetworkEntity _playerEntity;

        [Inject]
        private void Construct(IPlayerMoves moves, INetworkEntity playerEntity)
        {
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
            _moves.Max.View(lifetime, Recalculate);

            void Recalculate()
            {
                var max = _moves.Max.Value;
                var current = _moves.Current.Value;

                CheckObjects();
                AdjustPosition();
                SwitchPoints();

                void CheckObjects()
                {
                    if (_points.Count > max)
                    {
                        var delta = max - _points.Count;

                        for (var i = 0; i < delta; i++)
                        {
                            var point = _points[_points.Count - i - 1];
                            Destroy(point.gameObject);
                            _points.Remove(point);
                        }
                    }
                    else if (_points.Count < max)
                    {
                        var delta = max - _points.Count;

                        for (var i = 0; i < delta; i++)
                        {
                            var point = Instantiate(_pointPrefab, transform);
                            _points.Add(point);
                        }
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