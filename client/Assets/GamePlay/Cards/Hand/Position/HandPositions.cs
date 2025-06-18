using System;
using System.Collections.Generic;
using Global.Systems;
using Internal;
using UnityEngine;
using VContainer;

namespace GamePlay.Cards
{
    public interface IHandPositions
    {
        void AddCard(IReadOnlyLifetime lifetime, ICard card);
        ICardPositionHandle GetPositionHandle(ICard card);
    }
    
    [DisallowMultipleComponent]
    public class HandPositions : MonoBehaviour, IHandPositions, IUpdatable, IScopeSetup
    {
        [SerializeField] private HandPositionsOptions _options;
        [SerializeField] private Transform _center;
        [SerializeField] private List<Handle> _handles = new();

        private IUpdater _updater;

        private readonly Dictionary<ICard, Handle> _cardToHandle = new();

        [Inject]
        private void Construct(IUpdater updater)
        {
            _updater = updater;
        }

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _updater.Add(lifetime, this);
        }

        public void AddCard(IReadOnlyLifetime lifetime, ICard card)
        {
            var handle = new Handle(card.Transform);
            _handles.Add(handle);
            _cardToHandle.Add(card, handle);

            lifetime.Listen(() =>
            {
                _handles.Remove(handle);
                _cardToHandle.Remove(card);
            });
        }

        public ICardPositionHandle GetPositionHandle(ICard card)
        {
            return _cardToHandle[card];
        }

        public void OnUpdate(float delta)
        {
            UpdateEvaluation();
            UpdateWeight();
            UpdatePositions();
            UpdateRenderOrder();

            void UpdateEvaluation()
            {
                for (var i = 0; i < _handles.Count; i++)
                {
                    var handle = _handles[i];

                    if (_handles.Count == 1)
                    {
                        handle.Evaluation = 0.5f;
                        continue;
                    }

                    var rawEvaluation = i / (float)(_handles.Count - 1);
                    var evaluation = _options.EvaluationCurve.Evaluate(rawEvaluation);
                    handle.Evaluation = Mathf.Lerp(handle.Evaluation, evaluation, _options.MoveSpeed * delta);
                }
            }

            void UpdateWeight()
            {
                foreach (var handle in _handles)
                    handle.Weight = handle.Evaluation;

                for (var i = 0; i < _handles.Count; i++)
                {
                    var startHandle = _handles[i];
                    var cardForce = startHandle.Force;

                    for (var l = i - 1; l >= 0; l--)
                        CalculateWeight(l);

                    for (var r = i + 1; r < _handles.Count; r++)
                        CalculateWeight(r);

                    void CalculateWeight(int index)
                    {
                        var handle = _handles[index];
                        var forceEvaluation = _options.ForceCurve.Evaluate(handle.Evaluation);
                        var resultForce = forceEvaluation * cardForce;
                        var weight = handle.Weight;

                        if (index < i)
                            weight -= resultForce;
                        else
                            weight += resultForce;

                        handle.Weight = weight;
                    }
                }
            }

            void UpdatePositions()
            {
                var center = (Vector2)_center.position;
                var length = _handles.Count * _options.XSizeCurve.Evaluate(_handles.Count) * _options.CardXSize;

                var start = new Vector2(center.x - length / 2f, center.y);

                foreach (var handle in _handles)
                {
                    var xEvaluation = _options.XCurve.Evaluate(handle.Weight);
                    var yEvaluation = _options.YCurve.Evaluate(handle.Weight);

                    var position = start + new Vector2(xEvaluation * length, yEvaluation * _options.Magnitude);
                    handle.Position = Vector2.Lerp(handle.Position, position, _options.MoveSpeed * delta);

                    var rotationEvaluation = _options.RotationCurve.Evaluate(handle.Weight);
                    var addAngle = _options.AngleRange * rotationEvaluation;
                    handle.Rotation = Mathf.Lerp(handle.Rotation, addAngle, _options.MoveSpeed * delta);
                }
            }

            void UpdateRenderOrder()
            {
                for (var i = 0; i < _handles.Count; i++)
                    _handles[i].RenderOrder = i;
            }
        }

        [Serializable]
        public class Handle : ICardPositionHandle
        {
            public Handle(ICardTransform transform)
            {
                Transform = transform;
            }

            public ICardTransform Transform { get; }

            [SerializeField] public float Evaluation;
            [SerializeField] public float Weight;
            [SerializeField] public float Force => Transform.HandForce;

            [SerializeField] public Vector2 Position;
            [SerializeField] public float Rotation;
            [SerializeField] public int RenderOrder;

            public Vector2 SupposedPosition => Position;
            public float SupposedRotation => Rotation;
            public int SupposedRenderOrder => RenderOrder;
        }
    }
}