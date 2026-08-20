using Cysharp.Threading.Tasks;
using GamePlay.Players;
using Internal;
using Meta;
using Network;
using Shared;
using UnityEngine;

namespace GamePlay.Cards
{
    public interface ICardLocalDrag
    {
        UniTask Enter(ICardLocalIdle idle);
    }

    public class CardLocalDrag : ICardLocalDrag
    {
        public CardLocalDrag(
            INetworkConnection connection,
            IUpdater updater,
            ILocalCard card,
            IHandEntryHandle handEntryHandle,
            ICardTransform transform,
            ICardStateLifetime stateLifetime,
            ICardAction action,
            IPlayerTurns turns,
            ICardDefinition definition,
            CardDragOptions options)
        {
            _connection = connection;
            _updater = updater;
            _card = card;
            _handEntryHandle = handEntryHandle;
            _transform = transform;
            _stateLifetime = stateLifetime;
            _action = action;
            _turns = turns;
            _definition = definition;
            _options = options;
        }

        private readonly INetworkConnection _connection;
        private readonly IUpdater _updater;
        private readonly ILocalCard _card;
        private readonly IHandEntryHandle _handEntryHandle;
        private readonly ICardTransform _transform;
        private readonly ICardStateLifetime _stateLifetime;
        private readonly ICardAction _action;
        private readonly IPlayerTurns _turns;
        private readonly ICardDefinition _definition;
        private readonly CardDragOptions _options;

        public async UniTask Enter(ICardLocalIdle idle)
        {
            var lifetime = _stateLifetime.OccupyLifetime();
            var startPosition = _transform.Position;
            var startScale = _transform.Scale;
            var startRotation = _transform.Rotation;
            var startForce = _transform.HandForce;
            var positionHandle = _handEntryHandle.PositionHandle;
            var useLifetime = lifetime.Child();
            var transitionCurve = _options.TransitionCurve.CreateInstance();

            _turns.IsTurn.Advise(lifetime, isTurn => {
                if (isTurn == false)
                    useLifetime.Terminate();
            });

            _updater.RunUpdateAction(useLifetime, delta => {
                var evaluation = transitionCurve.StepForward(delta);
                var supposedRotation = positionHandle.SupposedRotation;

                var targetRotation = supposedRotation + _options.Rotation;
                var rotation = Mathf.LerpAngle(startRotation, targetRotation, evaluation);
                _transform.SetRotation(rotation);

                var force = Mathf.Lerp(startForce, _options.HandForce, evaluation);
                _transform.SetHandForce(force);

                var direction = new Angle(90 + supposedRotation).ToVector2();
                var targetPosition = positionHandle.SupposedPosition + direction * _options.MoveDistance;
                var position = Vector2.Lerp(startPosition, targetPosition, evaluation);
                _transform.SetPosition(position);

                var scale = Vector2.Lerp(startScale, Vector2.one * _options.Scale, evaluation);
                _transform.SetScale(scale);
            }).Forget();

            var useResult = await _action.TryUse(useLifetime);

            if (useResult.IsSuccess == true)
            {
                useResult.Payload.Type = _definition.Type;

                var requestResult = await _connection.Request(new SharedGameAction.CardUse()
                {
                    CardId = _card.Id,
                    Payload = useResult.Payload
                });

                if (requestResult.HasError == false)
                {
                    return;
                }
            }

            useLifetime.Terminate();

            await _updater.RunUpdateAction(lifetime, () => {
                    var distance = Vector2.Distance(_transform.Position, positionHandle.SupposedPosition);
                    return distance > 0.1f;
                },
                _ => MoveTowards(positionHandle.SupposedPosition));

            idle.Enter();
            return;

            void MoveTowards(Vector2 target)
            {
                var distanceToStart = Vector2.Distance(target, startPosition);
                var addForce = Mathf.Lerp(0, _options.HandForce, distanceToStart / _options.MaxForceDistance);
                var force = startForce + addForce;
                _transform.SetHandForce(force);
                _transform.SetPosition(target);
                _transform.SetRotation(positionHandle.SupposedRotation);
            }
        }
    }
}