using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using GamePlay.Boards;
using GamePlay.Cards;
using GamePlay.Loop;
using GamePlay.Services;
using Internal;
using Shared;
using UnityEngine;

namespace GamePlay
{
    public class CardActionSnapshotHandler : ISnapshotHandler<PlayerSnapshotRecord.CardUse>
    {
        public CardActionSnapshotHandler(
            IReadOnlyLifetime lifetime,
            IGameContext gameContext)
        {
            _lifetime = lifetime;
            _gameContext = gameContext;
        }

        private readonly IReadOnlyLifetime _lifetime;
        private readonly IGameContext _gameContext;

        public async UniTask Handle(PlayerSnapshotRecord.CardUse record)
        {
            var player = _gameContext.GetPlayer(record.PlayerId);

            Debug.Log(
                $"Handling card action snapshot for player {record.PlayerId}, card {record.CardId}, data {record.Data}");
            var card = player.Hand.Entries.First(t => t.Id == record.CardId)!;

            await PlayTargetAnimation(record.Data);
            await PlayActionAnimation(record.Data);

            await card.Use(_lifetime, record.Data);

            if (card is IRemoteCard rc)
            {
                UniTask.Create(async () => {
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
                }).Forget();
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

        private UniTask PlayTargetAnimation(ICardActionData data)
        {
            return PlayCellsAnimation(data, data.TargetCells, (visuals, lifetime) => visuals.PlayCellTarget(lifetime));
        }

        private UniTask PlayActionAnimation(ICardActionData data)
        {
            var positions = data.OpenedCells?.Select(o => o.Position).ToList();
            return PlayCellsAnimation(data, positions, (visuals, lifetime) => visuals.PlayCellAction(lifetime));
        }

        private async UniTask PlayCellsAnimation(
            ICardActionData data,
            IReadOnlyList<Position>? cells,
            Func<CellVisuals, IReadOnlyLifetime, UniTask> play)
        {
            if (cells == null || cells.Count == 0)
                return;

            var board = _gameContext.GetPlayer(data.TargetPlayer).Board;
            var tasks = new List<UniTask>();

            foreach (var position in cells)
            {
                var vector = position.ToVector();

                if (board.Cells.TryGetValue(vector, out var cell) == false)
                    continue;

                if (cell is CellView cellView)
                    tasks.Add(play(cellView.Visuals, _lifetime));
            }

            if (tasks.Count > 0)
                await UniTask.WhenAll(tasks);
        }
    }
}