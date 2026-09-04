using GamePlay.Boards;
using GamePlay.Loop;
using Internal;
using TMPro;

namespace GamePlay.UI
{
    public class BoardMinesCounterView : IGameStarted
    {
        public BoardMinesCounterView(IGameContext context, RoundOverlayUIBindings roundOverlay)
        {
            _context = context;

            var bindings = roundOverlay.Center.Mines;
            _ownText = bindings.Own.TextMeshProUGUI;
            _opponentText = bindings.Opponent.TextMeshProUGUI;
        }

        private readonly TMP_Text _ownText;
        private readonly TMP_Text _opponentText;

        private readonly IGameContext _context;

        public void OnGameStarted(IReadOnlyLifetime lifetime)
        {
            _ownText.text = "?";
            _opponentText.text = "?";

            Bind(_context.Self.Board, _ownText);
            Bind(_context.Other.Board, _opponentText);

            return;

            void Bind(IBoard board, TMP_Text text)
            {
                board.State.Advise(lifetime, state => {
                    text.text = (state.Mines - state.Flags).ToString();
                });
            }
        }
    }
}
