using Global.Systems;
using Internal;

namespace GamePlay.Cards
{
    public interface ICardRemoteIdle
    {
        void Enter();
    }
    
    public class CardRemoteIdle : ICardRemoteIdle
    {
        public CardRemoteIdle(
            IUpdater updater,
            IHandEntryHandle handEntryHandle,
            ICardTransform transform,
            ICardStateLifetime stateLifetime,
            ICardRenderer renderer,
            CardRemoteIdleOptions options)
        {
            _updater = updater;
            _handEntryHandle = handEntryHandle;
            _transform = transform;
            _stateLifetime = stateLifetime;
            _renderer = renderer;
            _options = options;
        }

        private readonly IUpdater _updater;
        private readonly IHandEntryHandle _handEntryHandle;
        private readonly ICardTransform _transform;
        private readonly ICardStateLifetime _stateLifetime;
        private readonly ICardRenderer _renderer;
        private readonly CardRemoteIdleOptions _options;

        public void Enter()
        {
            var lifetime = _stateLifetime.OccupyLifetime();
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