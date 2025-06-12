using Global.Systems;
using Internal;

namespace GamePlay.Cards
{
    public class CardRemoteIdle : ICardRemoteIdle
    {
        public CardRemoteIdle(
            IUpdater updater,
            IHandEntryHandle handEntryHandle,
            ICardTransform transform,
            ICardStateContext stateContext,
            ICardRenderer renderer,
            CardRemoteIdleOptions options)
        {
            _updater = updater;
            _handEntryHandle = handEntryHandle;
            _transform = transform;
            _stateContext = stateContext;
            _renderer = renderer;
            _options = options;
        }

        private readonly IUpdater _updater;
        private readonly IHandEntryHandle _handEntryHandle;
        private readonly ICardTransform _transform;
        private readonly ICardStateContext _stateContext;
        private readonly ICardRenderer _renderer;
        private readonly CardRemoteIdleOptions _options;

        public void Enter()
        {
            var lifetime = _stateContext.OccupyLifetime();
            var selectionCurve = _options.SelectionCurve.CreateInstance();
            var positionHandle = _handEntryHandle.PositionHandle;

            _updater.RunUpdateAction(lifetime, delta =>
            {
                var rotation = positionHandle.SupposedRotation;
                _transform.SetRotation(rotation);

                var rotationEvaluation = selectionCurve.StepForward(delta);
                var force = _options.SelectionForce * rotationEvaluation;
                _transform.SetHandForce(force);

                var direction = new Angle(90 + rotation).ToVector2();
                var move = direction * (_options.SelectionDistance * rotationEvaluation);
                var position = positionHandle.SupposedPosition;
                _transform.SetPosition(position + move);

                _renderer.SetSortingOrder(positionHandle.SupposedRenderOrder);
            });
        }
    }
}