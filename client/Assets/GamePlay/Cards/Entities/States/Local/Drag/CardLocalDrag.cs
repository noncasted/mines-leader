using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using GamePlay.Players;
using Global.Systems;
using Internal;
using Meta;
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
            IGameContext gameContext,
            IUpdater updater,
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
            _gameContext = gameContext;
            _updater = updater;
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

        private readonly IGameContext _gameContext;
        private readonly IUpdater _updater;
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
            var selectionLifetime = lifetime.Child();

            _moves.IsTurn.Advise(lifetime, isTurn =>
            {
                if (isTurn == false)
                    selectionLifetime.Terminate();
            });

            _updater.RunUpdateAction(selectionLifetime, _ => MoveTowards(startPosition)).Forget();

            // TODO: replace with command
            var isUsed = true;
            selectionLifetime.Terminate();
            
            if (isUsed == false)
            {
                await _updater.RunUpdateAction(lifetime, () =>
                    {
                        var distance = Vector2.Distance(_transform.Position, positionHandle.SupposedPosition);
                        return distance > 0.1f;
                    },
                    _ => MoveTowards(positionHandle.SupposedPosition));

                idle.Enter();
                return;
            }

            _gameContext.Self.Board.InvokeUpdated();
            _drop.Enter().Forget();

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