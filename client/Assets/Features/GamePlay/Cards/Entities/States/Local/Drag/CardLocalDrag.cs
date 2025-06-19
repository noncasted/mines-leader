using Cysharp.Threading.Tasks;
using GamePlay.Players;
using Global.Systems;
using Internal;
using Meta;
using UnityEngine;

namespace GamePlay.Cards
{
    public class CardLocalDrag : ICardLocalDrag
    {
        public CardLocalDrag(
            IUpdater updater,
            IPlayerMana mana,
            IHandEntryHandle handEntryHandle,
            ICardTransform transform,
            ICardStateContext stateContext,
            ICardAction action,
            ICardLocalDrop drop,
            IPlayerTurns turns,
            ICardDefinition definition,
            CardDragOptions options)
        {
            _updater = updater;
            _mana = mana;
            _handEntryHandle = handEntryHandle;
            _transform = transform;
            _stateContext = stateContext;
            _action = action;
            _drop = drop;
            _turns = turns;
            _definition = definition;
            _options = options;
        }

        private readonly IUpdater _updater;
        private readonly IPlayerMana _mana;
        private readonly IHandEntryHandle _handEntryHandle;
        private readonly ICardTransform _transform;
        private readonly ICardStateContext _stateContext;
        private readonly ICardAction _action;
        private readonly ICardLocalDrop _drop;
        private readonly IPlayerTurns _turns;
        private readonly ICardDefinition _definition;
        private readonly CardDragOptions _options;

        public async UniTask Enter(ICardLocalIdle idle)
        {
            var startPosition = _transform.Position;
            var lifetime = _stateContext.OccupyLifetime();
            var startForce = _transform.HandForce;
            var positionHandle = _handEntryHandle.PositionHandle;
            var selectionLifetime = lifetime.Child();

            _turns.IsTurn.Advise(lifetime, isTurn =>
            {
                if (isTurn == false)
                    selectionLifetime.Terminate();
            });

            _updater.RunUpdateAction(selectionLifetime, _ => MoveTowards(startPosition)).Forget();

            var isUsed = await _action.Execute(selectionLifetime);
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

            _mana.RemoveCurrent(_definition.ManaCost);
            _turns.OnUsed();
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