using Cysharp.Threading.Tasks;
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
            ICardStateLifetime stateLifetime,
            ICardPointerHandler pointerHandler,
            ICardLocalDrag drag,
            ICardRenderer renderer,
            ICardContext context)
        {
            _updater = updater;
            _handEntryHandle = handEntryHandle;
            _transform = transform;
            _stateLifetime = stateLifetime;
            _pointerHandler = pointerHandler;
            _drag = drag;
            _renderer = renderer;
            _context = context;
        }

        private readonly IUpdater _updater;
        private readonly IHandEntryHandle _handEntryHandle;
        private readonly ICardTransform _transform;
        private readonly ICardStateLifetime _stateLifetime;
        private readonly ICardPointerHandler _pointerHandler;
        private readonly ICardLocalDrag _drag;
        private readonly ICardRenderer _renderer;
        private readonly ICardContext _context;

        public void Enter()
        {
            var options = GamePlayAssets.CardIdleOptions;

            var lifetime = _stateLifetime.OccupyLifetime();
            var selectionCurve = options.SelectionCurve.CreateInstance();
            var positionHandle = _handEntryHandle.PositionHandle;

            _updater.RunUpdateAction(lifetime, delta => {
                var rotation = positionHandle.SupposedRotation;
                _transform.SetRotation(rotation);

                var rotationEvaluation = GetRotationEvaluation();
                var force = options.SelectionForce * rotationEvaluation;
                _transform.SetHandForce(force);

                var direction = new Angle(90 + rotation).ToVector2();
                var move = direction * (options.SelectionDistance * rotationEvaluation);
                var position = positionHandle.SupposedPosition;
                _transform.SetPosition(position + move);

                if (_pointerHandler.IsHovered.Value == true)
                    _renderer.SetSortingOrder(100);
                else
                    _renderer.SetSortingOrder(positionHandle.SupposedRenderOrder);

                var scale = options.ScaleCurve.Evaluate(selectionCurve.Progress);
                _transform.SetScale(Vector2.one * scale);
                return;

                float GetRotationEvaluation()
                {
                    if (_pointerHandler.IsHovered.Value == true)
                        return selectionCurve.StepForward(delta);

                    return selectionCurve.StepBack(delta);
                }
            });

            _pointerHandler.IsPressed.AdviseTrue(lifetime, () => {
                if (_context.IsAvailable.Value == false)
                    return;

                _drag.Enter(this).Forget();
            });
        }
    }
}