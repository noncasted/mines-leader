using Cysharp.Threading.Tasks;
using Global.Systems;
using Internal;
using UnityEngine;

namespace GamePlay.Cards
{
    public interface ICardLocalIdle
    {
        void Enter();
    }
    
    public class CardLocalIdle : ICardLocalIdle
    {
        public CardLocalIdle(
            IUpdater updater,
            IHandEntryHandle handEntryHandle,
            ICardTransform transform,
            ICardStateContext stateContext,
            ICardPointerHandler pointerHandler,
            ICardLocalDrag drag,
            ICardRenderer renderer,
            ICardActionState actionState,
            CardIdleOptions options)
        {
            _updater = updater;
            _handEntryHandle = handEntryHandle;
            _transform = transform;
            _stateContext = stateContext;
            _pointerHandler = pointerHandler;
            _drag = drag;
            _renderer = renderer;
            _actionState = actionState;
            _options = options;
        }

        private readonly IUpdater _updater;
        private readonly IHandEntryHandle _handEntryHandle;
        private readonly ICardTransform _transform;
        private readonly ICardStateContext _stateContext;
        private readonly ICardPointerHandler _pointerHandler;
        private readonly ICardLocalDrag _drag;
        private readonly ICardRenderer _renderer;
        private readonly ICardActionState _actionState;
        private readonly CardIdleOptions _options;

        public void Enter()
        {
            var lifetime = _stateContext.OccupyLifetime();
            var selectionCurve = _options.SelectionCurve.CreateInstance();
            var positionHandle = _handEntryHandle.PositionHandle;

            _updater.RunUpdateAction(lifetime, delta =>
            {
                var rotation = positionHandle.SupposedRotation;
                _transform.SetRotation(rotation);

                var rotationEvaluation = GetRotationEvaluation();
                var force = _options.SelectionForce * rotationEvaluation;
                _transform.SetHandForce(force);

                var direction = new Angle(90 + rotation).ToVector2();
                var move = direction * (_options.SelectionDistance * rotationEvaluation);
                var position = positionHandle.SupposedPosition;
                _transform.SetPosition(position + move);

                if (_pointerHandler.IsHovered.Value == true)
                    _renderer.SetSortingOrder(100);
                else
                    _renderer.SetSortingOrder(positionHandle.SupposedRenderOrder);

                var scale = _options.ScaleCurve.Evaluate(selectionCurve.Progress);
                _transform.SetScale(Vector2.one * scale);

                float GetRotationEvaluation()
                {
                    if (_pointerHandler.IsHovered.Value == true)
                        return selectionCurve.StepForward(delta);

                    return selectionCurve.StepBack(delta);
                }
            });

            _pointerHandler.IsPressed.AdviseTrue(lifetime, () =>
            {
                if (_actionState.IsAvailable.Value == false)
                    return;

                _drag.Enter(this).Forget();
            });
        }
    }
}