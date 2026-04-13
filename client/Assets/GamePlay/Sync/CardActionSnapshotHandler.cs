using System.Linq;
using Cysharp.Threading.Tasks;
using GamePlay.Cards;
using GamePlay.Loop;
using GamePlay.Services;
using GamePlay.UI.ActionLog;
using Internal;
using Shared;
using UnityEngine;

namespace GamePlay
{
    public class CardActionSnapshotHandler : ISnapshotHandler<PlayerSnapshotRecord.CardUse>
    {
        public CardActionSnapshotHandler(
            IReadOnlyLifetime lifetime,
            IGameContext gameContext,
            IGameActionLog actionLog)
        {
            _lifetime = lifetime;
            _gameContext = gameContext;
            _actionLog = actionLog;
        }

        private readonly IReadOnlyLifetime _lifetime;
        private readonly IGameContext _gameContext;
        private readonly IGameActionLog _actionLog;

        public async UniTask Handle(PlayerSnapshotRecord.CardUse record)
        {
            var player = _gameContext.GetPlayer(record.PlayerId);

            Debug.Log(
                $"Handling card action snapshot for player {record.PlayerId}, card {record.CardId}, data {record.Data}");
            var card = player.Hand.Entries.First(t => t.Id == record.CardId)!;

            _actionLog.LogCardAction(record.PlayerId, card.Type);

            await card.Use(_lifetime, record.Data);

            if (card is IRemoteCard rc)
            {
                var cardTransform = card.Transform;
                var startPos = cardTransform.Position;
                var direction = (cardTransform.Rotation + 90f).ToAngle().ToVector2() * -1f;
                var endPos = startPos + direction * 5f;

                rc.PrepareForDrop();
                rc.Reveal();

                var timer = 0f;
                var duration = 0.4f;

                while (timer < duration)
                {
                    timer += Time.deltaTime;
                    var t = Mathf.Clamp01(timer / duration);
                    cardTransform.SetPosition(Vector2.Lerp(startPos, endPos, t));
                    var xScale = Mathf.Cos(t * Mathf.PI * 2f);
                    cardTransform.SetScale(new Vector2(xScale, 1f));
                    await UniTask.Yield();
                }

                await UniTask.Delay(1000);
                await card.Destroy();
            }
            else
            {
                UniTask.Create(async () => {
                                   if (card is ILocalCard localCard)
                                       await localCard.Drop.Enter(card.Lifetime);

                                   await card.Destroy();
                               }
                           )
                       .Forget();
            }
        }
    }
}