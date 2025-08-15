using Common.Network;
using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using GamePlay.Players;
using Global.Backend;
using Global.Systems;
using Internal;
using Meta;
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
            IGameContext gameContext,
            IUpdater updater,
            INetworkEntity entity,
            IPlayerMana mana,
            IHandEntryHandle handEntryHandle,
            ICardTransform transform,
            ICardStateLifetime stateLifetime,
            ICardAction action,
            ICardLocalDrop drop,
            IPlayerMoves moves,
            ICardDefinition definition,
            CardDragOptions options)
        {
            _connection = connection;
            _gameContext = gameContext;
            _updater = updater;
            _entity = entity;
            _mana = mana;
            _handEntryHandle = handEntryHandle;
            _transform = transform;
            _stateLifetime = stateLifetime;
            _action = action;
            _drop = drop;
            _moves = moves;
            _definition = definition;
            _options = options;
        }

        private readonly INetworkConnection _connection;
        private readonly IGameContext _gameContext;
        private readonly IUpdater _updater;
        private readonly INetworkEntity _entity;
        private readonly IPlayerMana _mana;
        private readonly IHandEntryHandle _handEntryHandle;
        private readonly ICardTransform _transform;
        private readonly ICardStateLifetime _stateLifetime;
        private readonly ICardAction _action;
        private readonly ICardLocalDrop _drop;
        private readonly IPlayerMoves _moves;
        private readonly ICardDefinition _definition;
        private readonly CardDragOptions _options;

        public async UniTask Enter(ICardLocalIdle idle)
        {
            var startPosition = _transform.Position;
            var lifetime = _stateLifetime.OccupyLifetime();
            var startForce = _transform.HandForce;
            var positionHandle = _handEntryHandle.PositionHandle;
            var useLifetime = lifetime.Child();

            _moves.IsTurn.Advise(lifetime, isTurn =>
                {
                    if (isTurn == false)
                        useLifetime.Terminate();
                }
            );

            _updater.RunUpdateAction(useLifetime, _ => MoveTowards(startPosition)).Forget();

            var useResult = await _action.TryUse(useLifetime);

            if (useResult.IsSuccess == true)
            {
                var requestResult = await _connection.Request(new SharedGameAction.CardUse()
                    {
                        Index = _entity.Id,
                        Payload = useResult.Payload
                    }
                );
                
                if (requestResult.HasError == false)
                    return;
            }

            useLifetime.Terminate();

            await _updater.RunUpdateAction(lifetime, () =>
                {
                    var distance = Vector2.Distance(_transform.Position, positionHandle.SupposedPosition);
                    return distance > 0.1f;
                },
                _ => MoveTowards(positionHandle.SupposedPosition)
            );

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