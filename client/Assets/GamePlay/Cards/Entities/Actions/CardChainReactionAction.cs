using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GamePlay.Boards;
using Internal;
using Shared;
using UnityEngine;

namespace GamePlay.Cards
{
    public class CardChainReactionAction : ICardAction
    {
        public CardChainReactionAction(
            ICardDropArea dropArea,
            ICardPointerHandler pointerHandler,
            ICardContext context)
        {
            _dropArea = dropArea;
            _pointerHandler = pointerHandler;
            _context = context;
        }

        private readonly ICardDropArea _dropArea;
        private readonly ICardPointerHandler _pointerHandler;
        private readonly ICardContext _context;

        public async UniTask<CardActionResult> TryUse(IReadOnlyLifetime lifetime)
        {
            var selectionLifetime = _pointerHandler.GetUpAwaiterLifetime(lifetime);

            var pattern = new Pattern(_context.TargetBoard);
            var result = await _dropArea.Show(lifetime, selectionLifetime, pattern);

            return new CardActionResult()
            {
                IsSuccess = result.IsSuccess,
                Payload = new CardUsePayload.ChainReaction()
                {
                    Position = result.Position.ToPosition()
                }
            };
        }

        public class Snapshot : ICardActionSync<CardActionSnapshot.ChainReaction>
        {
            public UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.ChainReaction payload)
            {
                return UniTask.CompletedTask;
            }
        }

        public class Pattern : ICardDropPattern
        {
            public Pattern(IBoard board)
            {
                _board = board;
            }

            private readonly IBoard _board;

            public IReadOnlyList<IBoardCell> GetDropData(Vector2Int pointer)
            {
                return new[] { _board.Cells[pointer] };
            }
        }
    }
}
